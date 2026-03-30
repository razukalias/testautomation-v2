using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Excel : Component
    {
        public Excel()
        {
            Name = "Excel";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var data = new ExcelData
            {
                Id = this.Id,
                ComponentName = this.Name
            };

            try
            {
                // Get settings
                data.FilePath = GetSettingValue("FilePath", string.Empty);
                data.FileMode = GetSettingValue("FileMode", "Existing");
                data.Operation = GetSettingValue("Operation", "Write");
                data.SheetName = GetSettingValue("SheetName", string.Empty);
                data.SelectedSheet = GetSettingValue("SelectedSheet", string.Empty);
                data.JsonData = GetSettingValue("JsonData", string.Empty);
                
                // Get folder and file name for new file mode
                var folderPath = ResolveWithProjectVariables(GetSettingValue("FolderPath", string.Empty));
                var fileName = ResolveWithProjectVariables(GetSettingValue("FileName", string.Empty));

                // Resolve file path
                var filePath = ResolveWithProjectVariables(data.FilePath);
                
                bool isNewFile = string.Equals(data.FileMode, "New", StringComparison.OrdinalIgnoreCase);
                
                // For New file mode, combine FolderPath + FileName if FilePath is empty
                if (isNewFile && string.IsNullOrWhiteSpace(filePath))
                {
                    // Build path from folder + filename
                    if (!string.IsNullOrWhiteSpace(folderPath) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        filePath = Path.Combine(folderPath, fileName);
                    }
                    else if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        // Just filename - use current directory or temp
                        filePath = Path.Combine(Path.GetTempPath(), fileName);
                    }
                }
                
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    data.ErrorMessage = "File path is required. For new files, enter Folder Path and File Name. For existing files, browse or enter the File Path.";
                    data.Success = false;
                    return data;
                }
                
                // Ensure .xlsx extension for new files
                if (isNewFile && !Path.HasExtension(filePath))
                {
                    filePath = filePath + ".xlsx";
                }
                
                // Update the data model with the resolved path
                data.FilePath = filePath;

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Update isNewFile based on actual file existence
                isNewFile = isNewFile || !File.Exists(filePath);

                // Handle New File mode - create complete file from JSON
                if (isNewFile)
                {
                    await CreateNewFileFromJson(filePath, data, context);
                    
                    // Collect sheet names for UI
                    if (data.Success && File.Exists(filePath))
                    {
                        using var wb = new XLWorkbook(filePath);
                        data.SheetNames = wb.Worksheets.Select(s => s.Name).ToList();
                    }
                    
                    return data;
                }

                // Handle Existing File mode
                using var workbook = new XLWorkbook(filePath);

                // Collect sheet names for UI dropdown
                data.SheetNames = workbook.Worksheets.Select(s => s.Name).ToList();

                // Get selected sheet or default to first
                var sheetName = !string.IsNullOrWhiteSpace(data.SelectedSheet) ? data.SelectedSheet : data.SheetName;
                IXLWorksheet worksheet = null;
                
                if (!string.IsNullOrWhiteSpace(sheetName))
                {
                    worksheet = workbook.Worksheets.FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase));
                }
                
                if (worksheet == null)
                {
                    worksheet = workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        worksheet = workbook.Worksheets.Add("Sheet1");
                    }
                }

                // Perform operation based on JSON data
                switch (data.Operation.ToLower())
                {
                    case "write":
                        await WriteAsync(worksheet, data, context);
                        break;
                    case "append":
                        await AppendAsync(worksheet, data, context);
                        break;
                    case "createsheet":
                        await CreateSheetAsync(workbook, data, context);
                        break;
                    case "deleterows":
                        await DeleteRowsAsync(worksheet, data, context);
                        break;
                    case "deletecolumns":
                        await DeleteColumnsAsync(worksheet, data, context);
                        break;
                    case "clearcells":
                        await ClearCellsAsync(worksheet, data, context);
                        break;
                    default:
                        data.ErrorMessage = $"Unknown operation: {data.Operation}";
                        data.Success = false;
                        break;
                }

                // Save workbook if operation succeeded
                if (data.Success)
                {
                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                data.ErrorMessage = ex.Message;
                data.Success = false;
            }

            return data;
        }

        /// <summary>
        /// Creates a new Excel file from JSON structure
        /// Format: {"sheets":[{"name":"Sheet1","headers":["A","B"],"rows":[["v1","v2"]]}]}
        /// </summary>
        private async Task CreateNewFileFromJson(string filePath, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    // If no JSON but we have a sheet name, create blank file with that sheet
                    if (!string.IsNullOrWhiteSpace(data.SheetName))
                    {
                        using var blankWorkbook = new XLWorkbook();
                        blankWorkbook.Worksheets.Add(data.SheetName);
                        blankWorkbook.SaveAs(filePath);
                        data.Result = $"New file created with sheet '{data.SheetName}'";
                        data.Success = true;
                        return;
                    }
                    
                    data.ErrorMessage = "Enter JSON data in the Data field, or specify a Sheet Name.\n\nExample JSON:\n{\"sheets\":[{\"name\":\"Sheet1\",\"headers\":[\"A\",\"B\"],\"rows\":[[\"v1\",\"v2\"]]}]}";
                    data.Success = false;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var newFileData = JsonSerializer.Deserialize<NewFileJsonData>(data.JsonData, options);
                
                if (newFileData == null || newFileData.Sheets == null || newFileData.Sheets.Count == 0)
                {
                    data.ErrorMessage = "Invalid JSON: must contain at least one sheet with 'name' and 'rows'";
                    data.Success = false;
                    return;
                }

                using var workbook = new XLWorkbook();

                foreach (var sheetData in newFileData.Sheets)
                {
                    var sheetName = string.IsNullOrWhiteSpace(sheetData.Name) ? "Sheet1" : sheetData.Name;
                    var worksheet = workbook.Worksheets.Add(sheetName);

                    // Write headers if provided
                    if (sheetData.Headers != null && sheetData.Headers.Count > 0)
                    {
                        for (int col = 0; col < sheetData.Headers.Count; col++)
                        {
                            worksheet.Cell(1, col + 1).Value = sheetData.Headers[col] ?? string.Empty;
                        }

                        // Write data rows starting from row 2
                        if (sheetData.Rows != null)
                        {
                            for (int row = 0; row < sheetData.Rows.Count; row++)
                            {
                                var rowData = sheetData.Rows[row];
                                for (int col = 0; col < rowData.Count; col++)
                                {
                                    worksheet.Cell(row + 2, col + 1).Value = ResolveWithProjectVariables(rowData[col] ?? string.Empty);
                                }
                            }
                        }
                    }
                    else if (sheetData.Rows != null && sheetData.Rows.Count > 0)
                    {
                        // No headers - write data starting from row 1
                        for (int row = 0; row < sheetData.Rows.Count; row++)
                        {
                            var rowData = sheetData.Rows[row];
                            for (int col = 0; col < rowData.Count; col++)
                            {
                                worksheet.Cell(row + 1, col + 1).Value = ResolveWithProjectVariables(rowData[col] ?? string.Empty);
                            }
                        }
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();
                }

                workbook.SaveAs(filePath);
                
                data.Result = $"New file created with {newFileData.Sheets.Count} sheet(s)";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Write operation - writes data at specified cell or appends based on mode
        /// Format: {"startCell":"A1","values":[["v1","v2"]]} or {"mode":"append","startColumn":"A","values":[["v1","v2"]]}
        /// </summary>
        private async Task WriteAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    data.ErrorMessage = "JSON data is required for Write operation";
                    data.Success = false;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var writeData = JsonSerializer.Deserialize<WriteJsonData>(data.JsonData, options);
                
                if (writeData?.Values == null || writeData.Values.Count == 0)
                {
                    data.ErrorMessage = "Invalid JSON: must contain 'values' array";
                    data.Success = false;
                    return;
                }

                int startRow;
                int startCol;

                // Check if append mode
                bool isAppend = string.Equals(writeData.Mode, "append", StringComparison.OrdinalIgnoreCase);
                
                if (isAppend)
                {
                    // Append mode - start after last used row
                    startRow = worksheet.LastRowUsed()?.RowNumber() + 1 ?? 1;
                    startCol = !string.IsNullOrWhiteSpace(writeData.StartColumn) 
                        ? ColumnLetterToIndex(writeData.StartColumn) 
                        : 1;
                }
                else
                {
                    // Write mode - use startCell
                    if (string.IsNullOrWhiteSpace(writeData.StartCell))
                    {
                        data.ErrorMessage = "startCell is required for Write operation";
                        data.Success = false;
                        return;
                    }

                    var cellRef = ParseCellReference(writeData.StartCell);
                    startRow = cellRef.Row;
                    startCol = cellRef.Column;
                }

                // Write values
                for (int i = 0; i < writeData.Values.Count; i++)
                {
                    var row = writeData.Values[i];
                    for (int j = 0; j < row.Count; j++)
                    {
                        var cell = worksheet.Cell(startRow + i, startCol + j);
                        cell.Value = ResolveWithProjectVariables(row[j] ?? string.Empty);
                    }
                }

                data.Result = isAppend 
                    ? $"Rows appended starting at row {startRow}, {writeData.Values.Count} rows" 
                    : $"Range written at cell, {writeData.Values.Count} rows";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Append operation - always appends after last row
        /// Format: {"startColumn":"A","values":[["v1","v2"]]}
        /// </summary>
        private async Task AppendAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    data.ErrorMessage = "JSON data is required for Append operation";
                    data.Success = false;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var appendData = JsonSerializer.Deserialize<AppendJsonData>(data.JsonData, options);
                
                if (appendData?.Values == null || appendData.Values.Count == 0)
                {
                    data.ErrorMessage = "Invalid JSON: must contain 'values' array";
                    data.Success = false;
                    return;
                }

                int startRow = worksheet.LastRowUsed()?.RowNumber() + 1 ?? 1;
                int startCol = !string.IsNullOrWhiteSpace(appendData.StartColumn) 
                    ? ColumnLetterToIndex(appendData.StartColumn) 
                    : 1;

                for (int i = 0; i < appendData.Values.Count; i++)
                {
                    var row = appendData.Values[i];
                    for (int j = 0; j < row.Count; j++)
                    {
                        var cell = worksheet.Cell(startRow + i, startCol + j);
                        cell.Value = ResolveWithProjectVariables(row[j] ?? string.Empty);
                    }
                }

                data.Result = $"Rows appended starting at row {startRow}, {appendData.Values.Count} rows";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Creates a new sheet
        /// Format: {"name":"NewSheet"}
        /// </summary>
        private async Task CreateSheetAsync(XLWorkbook workbook, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    // Fall back to SheetName setting if no JSON
                    var sheetName = data.SheetName;
                    if (string.IsNullOrWhiteSpace(sheetName))
                    {
                        data.ErrorMessage = "Sheet name is required";
                        data.Success = false;
                        return;
                    }

                    var existing = workbook.Worksheets.FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        data.ErrorMessage = $"Sheet '{sheetName}' already exists";
                        data.Success = false;
                        return;
                    }

                    workbook.Worksheets.Add(sheetName);
                    data.Result = $"Sheet '{sheetName}' created";
                    data.Success = true;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var sheetData = JsonSerializer.Deserialize<CreateSheetJsonData>(data.JsonData, options);
                
                if (string.IsNullOrWhiteSpace(sheetData?.Name))
                {
                    data.ErrorMessage = "Invalid JSON: 'name' is required";
                    data.Success = false;
                    return;
                }

                var existingSheet = workbook.Worksheets.FirstOrDefault(s => 
                    string.Equals(s.Name, sheetData.Name, StringComparison.OrdinalIgnoreCase));
                
                if (existingSheet != null)
                {
                    data.ErrorMessage = $"Sheet '{sheetData.Name}' already exists";
                    data.Success = false;
                    return;
                }

                workbook.Worksheets.Add(sheetData.Name);
                data.Result = $"Sheet '{sheetData.Name}' created";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Deletes rows and shifts remaining rows up
        /// Format: {"startRow":1,"endRow":5}
        /// </summary>
        private async Task DeleteRowsAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    data.ErrorMessage = "JSON data is required for DeleteRows operation";
                    data.Success = false;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var deleteData = JsonSerializer.Deserialize<DeleteRowsJsonData>(data.JsonData, options);
                
                if (deleteData == null || deleteData.StartRow <= 0 || deleteData.EndRow <= 0)
                {
                    data.ErrorMessage = "Invalid JSON: 'startRow' and 'endRow' are required";
                    data.Success = false;
                    return;
                }

                if (deleteData.StartRow > deleteData.EndRow)
                {
                    data.ErrorMessage = "Start row must be less than or equal to end row";
                    data.Success = false;
                    return;
                }

                // Delete rows (this actually removes rows and shifts up, like Excel)
                worksheet.Rows(deleteData.StartRow, deleteData.EndRow).Delete();

                data.Result = $"Rows {deleteData.StartRow}-{deleteData.EndRow} deleted";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Deletes columns and shifts remaining columns left
        /// Format: {"startColumn":"A","endColumn":"C"}
        /// </summary>
        private async Task DeleteColumnsAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    data.ErrorMessage = "JSON data is required for DeleteColumns operation";
                    data.Success = false;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var deleteData = JsonSerializer.Deserialize<DeleteColumnsJsonData>(data.JsonData, options);
                
                if (deleteData == null || string.IsNullOrWhiteSpace(deleteData.StartColumn) || string.IsNullOrWhiteSpace(deleteData.EndColumn))
                {
                    data.ErrorMessage = "Invalid JSON: 'startColumn' and 'endColumn' are required";
                    data.Success = false;
                    return;
                }

                int startCol = ColumnLetterToIndex(deleteData.StartColumn);
                int endCol = ColumnLetterToIndex(deleteData.EndColumn);

                if (startCol > endCol)
                {
                    data.ErrorMessage = "Start column must be less than or equal to end column";
                    data.Success = false;
                    return;
                }

                // Delete columns (this actually removes columns and shifts left, like Excel)
                worksheet.Columns(startCol, endCol).Delete();

                data.Result = $"Columns {deleteData.StartColumn}-{deleteData.EndColumn} deleted";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Clears cell content without removing rows/columns
        /// Format: {"startCell":"A1","endCell":"C10"}
        /// </summary>
        private async Task ClearCellsAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.JsonData))
                {
                    data.ErrorMessage = "JSON data is required for ClearCells operation";
                    data.Success = false;
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var clearData = JsonSerializer.Deserialize<ClearCellsJsonData>(data.JsonData, options);
                
                if (string.IsNullOrWhiteSpace(clearData?.StartCell))
                {
                    data.ErrorMessage = "Invalid JSON: 'startCell' is required";
                    data.Success = false;
                    return;
                }

                var startRef = ParseCellReference(clearData.StartCell);
                
                // If endCell not specified, clear just the start cell
                if (string.IsNullOrWhiteSpace(clearData.EndCell))
                {
                    worksheet.Cell(startRef.Row, startRef.Column).Clear();
                    data.Result = $"Cell {clearData.StartCell} cleared";
                    data.Success = true;
                    return;
                }

                var endRef = ParseCellReference(clearData.EndCell);

                if (startRef.Row > endRef.Row || startRef.Column > endRef.Column)
                {
                    data.ErrorMessage = "Invalid range: start must be before end";
                    data.Success = false;
                    return;
                }

                // Clear cells in the range
                var range = worksheet.Range(startRef.Row, startRef.Column, endRef.Row, endRef.Column);
                range.Clear();

                data.Result = $"Range {clearData.StartCell}:{clearData.EndCell} cleared";
                data.Success = true;
            }
            catch (JsonException ex)
            {
                data.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                data.Success = false;
            }
        }

        /// <summary>
        /// Parses cell reference like "A1" or "BC123" into row and column indices
        /// </summary>
        private (int Row, int Column) ParseCellReference(string cellRef)
        {
            if (string.IsNullOrWhiteSpace(cellRef))
                return (1, 1);

            cellRef = cellRef.Trim().ToUpperInvariant();
            
            int colEnd = 0;
            while (colEnd < cellRef.Length && char.IsLetter(cellRef[colEnd]))
            {
                colEnd++;
            }

            if (colEnd == 0)
                return (1, 1);

            string colPart = cellRef.Substring(0, colEnd);
            string rowPart = cellRef.Substring(colEnd);

            int column = ColumnLetterToIndex(colPart);
            int row = int.TryParse(rowPart, out var r) ? r : 1;

            return (row, column);
        }

        /// <summary>
        /// Converts Excel column letter to 1-based index (A=1, B=2, ..., Z=26, AA=27, etc.)
        /// </summary>
        private int ColumnLetterToIndex(string letter)
        {
            if (string.IsNullOrWhiteSpace(letter))
                return 1;

            // If it's a number, parse directly
            if (int.TryParse(letter, out int index))
                return index;

            // Convert Excel column letter to 1-based index
            int result = 0;
            foreach (char c in letter.ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z')
                    throw new ArgumentException($"Invalid column letter: {letter}");
                result = result * 26 + (c - 'A' + 1);
            }
            return result;
        }

        private string GetSettingValue(string key, string fallback)
        {
            return Settings.TryGetValue(key, out var value) ? value : fallback;
        }

        private string ResolveWithProjectVariables(string input)
        {
            // Variable substitution is handled by VariableService before reaching component
            return input;
        }
    }

    #region JSON Data Models

    /// <summary>
    /// Model for new file JSON: {"sheets":[{"name":"Sheet1","headers":["A","B"],"rows":[["v1","v2"]]}]}
    /// </summary>
    public class NewFileJsonData
    {
        [JsonPropertyName("sheets")]
        public List<NewFileSheetData> Sheets { get; set; }
    }

    public class NewFileSheetData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("headers")]
        public List<string> Headers { get; set; }

        [JsonPropertyName("rows")]
        public List<List<string>> Rows { get; set; }
    }

    /// <summary>
    /// Model for write JSON: {"startCell":"A1","values":[["v1","v2"]]} or {"mode":"append","startColumn":"A","values":[["v1","v2"]]}
    /// </summary>
    public class WriteJsonData
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("startCell")]
        public string StartCell { get; set; }

        [JsonPropertyName("startColumn")]
        public string StartColumn { get; set; }

        [JsonPropertyName("values")]
        public List<List<string>> Values { get; set; }
    }

    /// <summary>
    /// Model for append JSON: {"startColumn":"A","values":[["v1","v2"]]}
    /// </summary>
    public class AppendJsonData
    {
        [JsonPropertyName("startColumn")]
        public string StartColumn { get; set; }

        [JsonPropertyName("values")]
        public List<List<string>> Values { get; set; }
    }

    /// <summary>
    /// Model for create sheet JSON: {"name":"NewSheet"}
    /// </summary>
    public class CreateSheetJsonData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    /// <summary>
    /// Model for delete rows JSON: {"startRow":1,"endRow":5}
    /// </summary>
    public class DeleteRowsJsonData
    {
        [JsonPropertyName("startRow")]
        public int StartRow { get; set; }

        [JsonPropertyName("endRow")]
        public int EndRow { get; set; }
    }

    /// <summary>
    /// Model for delete columns JSON: {"startColumn":"A","endColumn":"C"}
    /// </summary>
    public class DeleteColumnsJsonData
    {
        [JsonPropertyName("startColumn")]
        public string StartColumn { get; set; }

        [JsonPropertyName("endColumn")]
        public string EndColumn { get; set; }
    }

    /// <summary>
    /// Model for clear cells JSON: {"startCell":"A1","endCell":"C10"}
    /// </summary>
    public class ClearCellsJsonData
    {
        [JsonPropertyName("startCell")]
        public string StartCell { get; set; }

        [JsonPropertyName("endCell")]
        public string EndCell { get; set; }
    }

    #endregion
}
