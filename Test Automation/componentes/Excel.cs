using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                // Get settings (already resolved by VariableService)
                data.Operation = GetSettingValue("Operation", "WriteCell");
                data.FilePath = GetSettingValue("FilePath", string.Empty);
                data.FileMode = GetSettingValue("FileMode", "Existing");
                data.SheetName = GetSettingValue("SheetName", string.Empty);
                data.Column = GetSettingValue("Column", "A");
                data.Row = int.TryParse(GetSettingValue("Row", "1"), out var row) ? row : 1;
                data.Value = GetSettingValue("Value", string.Empty);
                data.Values = GetSettingValue("Values", "[]");
                data.DeleteStartColumn = GetSettingValue("DeleteStartColumn", "A");
                data.DeleteStartRow = int.TryParse(GetSettingValue("DeleteStartRow", "1"), out var dsr) ? dsr : 1;
                data.DeleteEndColumn = GetSettingValue("DeleteEndColumn", "A");
                data.DeleteEndRow = int.TryParse(GetSettingValue("DeleteEndRow", "1"), out var der) ? der : 1;

                // Resolve file path
                var filePath = ResolveWithProjectVariables(data.FilePath);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    data.ErrorMessage = "File path is required";
                    data.Success = false;
                    return data;
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XLWorkbook workbook;
                bool isNewFile = string.Equals(data.FileMode, "New", StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath);

                if (isNewFile)
                {
                    // Create new workbook
                    workbook = new XLWorkbook();
                    // Ensure sheet name is provided
                    if (string.IsNullOrWhiteSpace(data.SheetName))
                    {
                        data.ErrorMessage = "Sheet name is required for new Excel file";
                        data.Success = false;
                        return data;
                    }
                }
                else
                {
                    // Open existing workbook
                    workbook = new XLWorkbook(filePath);
                }

                // Get or create worksheet
                IXLWorksheet worksheet = null;
                if (!string.IsNullOrWhiteSpace(data.SheetName))
                {
                    worksheet = workbook.Worksheets.FirstOrDefault(s => string.Equals(s.Name, data.SheetName, StringComparison.OrdinalIgnoreCase));
                }
                if (worksheet == null)
                {
                    // If sheet not found, create new sheet (for new file) or use first sheet (for existing)
                    if (isNewFile)
                    {
                        worksheet = workbook.Worksheets.Add(data.SheetName);
                    }
                    else
                    {
                        worksheet = workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null)
                        {
                            worksheet = workbook.Worksheets.Add("Sheet1");
                        }
                    }
                }

                // Collect sheet names for UI dropdown
                data.SheetNames = workbook.Worksheets.Select(s => s.Name).ToList();

                // Perform operation
                switch (data.Operation.ToLower())
                {
                    case "writecell":
                        await WriteCellAsync(worksheet, data, context);
                        break;
                    case "writerange":
                        await WriteRangeAsync(worksheet, data, context);
                        break;
                    case "appendrow":
                        await AppendRowAsync(worksheet, data, context);
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
                if (data.Success && !string.Equals(data.Operation, "CreateSheet", StringComparison.OrdinalIgnoreCase))
                {
                    workbook.SaveAs(filePath);
                }

                workbook.Dispose();
            }
            catch (Exception ex)
            {
                data.ErrorMessage = ex.Message;
                data.Success = false;
            }

            return data;
        }

        private async Task WriteCellAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            var cell = worksheet.Cell(data.Row, ColumnLetterToIndex(data.Column));
            cell.Value = ResolveWithProjectVariables(data.Value);
            data.Result = $"Cell {data.Column}{data.Row} written";
            data.Success = true;
        }

        private async Task WriteRangeAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                var values = JsonSerializer.Deserialize<List<List<string>>>(data.Values);
                if (values == null || values.Count == 0)
                {
                    data.ErrorMessage = "Invalid JSON array for Values";
                    data.Success = false;
                    return;
                }

                int startRow = data.Row;
                int startCol = ColumnLetterToIndex(data.Column);

                for (int i = 0; i < values.Count; i++)
                {
                    var row = values[i];
                    for (int j = 0; j < row.Count; j++)
                    {
                        var cell = worksheet.Cell(startRow + i, startCol + j);
                        cell.Value = ResolveWithProjectVariables(row[j]);
                    }
                }

                data.Result = $"Range written starting at {data.Column}{data.Row}, {values.Count} rows";
                data.Success = true;
            }
            catch (JsonException)
            {
                data.ErrorMessage = "Values must be a JSON array of arrays (rows)";
                data.Success = false;
            }
        }

        private async Task AppendRowAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            try
            {
                var values = JsonSerializer.Deserialize<List<List<string>>>(data.Values);
                if (values == null || values.Count == 0)
                {
                    data.ErrorMessage = "Invalid JSON array for Values";
                    data.Success = false;
                    return;
                }

                int startRow = worksheet.LastRowUsed()?.RowNumber() + 1 ?? 1;
                int startCol = ColumnLetterToIndex(data.Column);

                for (int i = 0; i < values.Count; i++)
                {
                    var row = values[i];
                    for (int j = 0; j < row.Count; j++)
                    {
                        var cell = worksheet.Cell(startRow + i, startCol + j);
                        cell.Value = ResolveWithProjectVariables(row[j]);
                    }
                }

                data.Result = $"Rows appended starting at row {startRow}, {values.Count} rows";
                data.Success = true;
            }
            catch (JsonException)
            {
                data.ErrorMessage = "Values must be a JSON array of arrays (rows)";
                data.Success = false;
            }
        }

        private async Task CreateSheetAsync(XLWorkbook workbook, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
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
        }

        private async Task DeleteRowsAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            int startRow = data.DeleteStartRow;
            int endRow = data.DeleteEndRow;
            if (startRow > endRow)
            {
                data.ErrorMessage = "Start row must be less than or equal to end row";
                data.Success = false;
                return;
            }

            // Clear content of rows (do not shift)
            for (int row = startRow; row <= endRow; row++)
            {
                worksheet.Row(row).Clear();
            }

            data.Result = $"Rows {startRow}-{endRow} cleared";
            data.Success = true;
        }

        private async Task DeleteColumnsAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            int startCol = ColumnLetterToIndex(data.DeleteStartColumn);
            int endCol = ColumnLetterToIndex(data.DeleteEndColumn);
            if (startCol > endCol)
            {
                data.ErrorMessage = "Start column must be less than or equal to end column";
                data.Success = false;
                return;
            }

            // Clear content of columns (do not shift)
            for (int col = startCol; col <= endCol; col++)
            {
                worksheet.Column(col).Clear();
            }

            data.Result = $"Columns {data.DeleteStartColumn}-{data.DeleteEndColumn} cleared";
            data.Success = true;
        }

        private async Task ClearCellsAsync(IXLWorksheet worksheet, ExcelData data, Test_Automation.Models.ExecutionContext context)
        {
            // Clear cells in the rectangle defined by (data.Column, data.Row) to (data.DeleteEndColumn, data.DeleteEndRow)
            int startCol = ColumnLetterToIndex(data.Column);
            int startRow = data.Row;
            int endCol = ColumnLetterToIndex(data.DeleteEndColumn);
            int endRow = data.DeleteEndRow;

            if (startCol > endCol || startRow > endRow)
            {
                data.ErrorMessage = "Invalid range for clear";
                data.Success = false;
                return;
            }

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    worksheet.Cell(row, col).Clear();
                }
            }

            data.Result = $"Cells cleared from {data.Column}{data.Row} to {data.DeleteEndColumn}{data.DeleteEndRow}";
            data.Success = true;
        }

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
            // Simple variable substitution - in real implementation this should use the project's variable service
            // For now, just return input as is; the VariableService will handle substitution before reaching component
            return input;
        }
    }
}