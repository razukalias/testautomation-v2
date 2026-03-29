using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Componentes
{
    public class FileComponent : Component
    {
        public FileComponent()
        {
            Name = "File";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var data = new FileData
            {
                Id = this.Id,
                ComponentName = this.Name
            };

            try
            {
                // Get settings (already resolved by VariableService)
                data.Operation = GetSettingValue("Operation", "Read");
                var sourcePath = GetSettingValue("SourcePath", string.Empty);
                var destinationPath = GetSettingValue("DestinationPath", string.Empty);
                var destinationFolder = GetSettingValue("DestinationFolder", string.Empty);
                var destinationFileName = GetSettingValue("DestinationFileName", string.Empty);
                var content = GetSettingValue("Content", string.Empty);
                var encodingName = GetSettingValue("Encoding", "UTF-8");
                var overwrite = bool.TryParse(GetSettingValue("Overwrite", "false"), out var ow) && ow;
                var append = bool.TryParse(GetSettingValue("Append", "false"), out var ap) && ap;
                var fileFilter = GetSettingValue("FileFilter", "*.*");
                var outputVariable = GetSettingValue("OutputVariable", string.Empty);
                var readMode = GetSettingValue("ReadMode", "All");
                var selectedFilePathsJson = GetSettingValue("SelectedFilePaths", "[]");
                var recursive = bool.TryParse(GetSettingValue("Recursive", "false"), out var rec) && rec;
                var includeMetadata = bool.TryParse(GetSettingValue("IncludeMetadata", "false"), out var im) && im;
                
                // Parse selected file paths
                List<string> selectedFilePaths = new List<string>();
                try
                {
                    selectedFilePaths = System.Text.Json.JsonSerializer.Deserialize<List<string>>(selectedFilePathsJson) ?? new List<string>();
                }
                catch { }

                data.SourcePath = sourcePath;
                // Compute DestinationPath from folder/filename if not provided
                if (string.IsNullOrEmpty(destinationPath) && (!string.IsNullOrEmpty(destinationFolder) || !string.IsNullOrEmpty(destinationFileName)))
                {
                    destinationPath = Path.Combine(destinationFolder ?? "", destinationFileName ?? "");
                }
                data.DestinationPath = destinationPath;
                data.DestinationFolder = destinationFolder;
                data.DestinationFileName = destinationFileName;
                data.Content = content;
                data.Encoding = encodingName;
                data.Overwrite = overwrite;
                data.Append = append;
                data.FileFilter = fileFilter;
                data.OutputVariable = outputVariable;
                data.ReadMode = readMode;
                data.SelectedFilePaths = selectedFilePaths;
                data.Recursive = recursive;
                data.IncludeMetadata = includeMetadata;

                var encoding = GetEncoding(encodingName);

                switch (data.Operation.ToLower())
                {
                    case "read":
                        await ReadFileAsync(data, sourcePath, encoding, includeMetadata, context);
                        break;
                    case "write":
                        await WriteFileAsync(data, sourcePath, content, encoding, overwrite, append, context);
                        break;
                    case "copy":
                        await CopyFileAsync(data, sourcePath, destinationPath, overwrite, context);
                        break;
                    case "move":
                        await MoveFileAsync(data, sourcePath, destinationPath, overwrite, context);
                        break;
                    case "delete":
                        await DeleteFileAsync(data, sourcePath, context);
                        break;
                    case "list":
                        await ListFilesAsync(data, sourcePath, fileFilter, recursive, includeMetadata, context);
                        break;
                    case "createfolder":
                        await CreateFolderAsync(data, sourcePath, context);
                        break;
                    case "createfile":
                        await CreateFileAsync(data, sourcePath, content, encoding, overwrite, context);
                        break;
                    case "readfiles":
                    case "readallfiles":
                        await ReadFilesAsync(data, readMode, selectedFilePaths, sourcePath, fileFilter, recursive, encoding, includeMetadata, context);
                        break;
                    default:
                        data.ErrorMessage = $"Unknown operation: {data.Operation}";
                        data.Success = false;
                        break;
                }

                // Populate properties for assertions
                data.Properties["content"] = data.Result;
                data.Properties["success"] = data.Success;
                data.Properties["files"] = data.Files;
                data.Properties["fileCount"] = data.Files.Count;
                data.Properties["metadata"] = data.Metadata;
                data.Properties["errorMessage"] = data.ErrorMessage ?? string.Empty;

                // Store result in variable if specified
                if (data.Success && !string.IsNullOrEmpty(outputVariable))
                {
                    context.Variables[outputVariable] = data.Result;
                }
            }
            catch (Exception ex)
            {
                data.ErrorMessage = ex.Message;
                data.Success = false;
            }

            return data;
        }

        private async Task ReadFileAsync(FileData data, string path, Encoding encoding, bool includeMetadata, ExecutionContext context)
        {
            if (!File.Exists(path))
            {
                data.ErrorMessage = $"File not found: {path}";
                data.Success = false;
                return;
            }

            data.Result = await File.ReadAllTextAsync(path, encoding, context.StopToken);
            data.Success = true;

            if (includeMetadata)
            {
                var fileInfo = new FileInfo(path);
                var meta = new Dictionary<string, object>
                {
                    ["FullName"] = fileInfo.FullName,
                    ["Name"] = fileInfo.Name,
                    ["Extension"] = fileInfo.Extension,
                    ["Length"] = fileInfo.Length,
                    ["CreationTime"] = fileInfo.CreationTime,
                    ["LastAccessTime"] = fileInfo.LastAccessTime,
                    ["LastWriteTime"] = fileInfo.LastWriteTime,
                    ["Attributes"] = fileInfo.Attributes.ToString()
                };
                data.Metadata.Add(meta);
            }
        }

        private async Task WriteFileAsync(FileData data, string path, string content, Encoding encoding, bool overwrite, bool append, ExecutionContext context)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (append)
            {
                // Append mode - ignore overwrite flag
                await File.AppendAllTextAsync(path, content, encoding, context.StopToken);
            }
            else
            {
                // Overwrite mode
                if (File.Exists(path) && !overwrite)
                {
                    data.ErrorMessage = $"File already exists and overwrite is false: {path}";
                    data.Success = false;
                    return;
                }
                await File.WriteAllTextAsync(path, content, encoding, context.StopToken);
            }
            data.Result = path;
            data.Success = true;
        }

        private async Task CopyFileAsync(FileData data, string source, string destination, bool overwrite, ExecutionContext context)
        {
            if (!File.Exists(source))
            {
                data.ErrorMessage = $"Source file not found: {source}";
                data.Success = false;
                return;
            }

            if (File.Exists(destination) && !overwrite)
            {
                data.ErrorMessage = $"Destination file already exists and overwrite is false: {destination}";
                data.Success = false;
                return;
            }

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(source, destination, overwrite);
            data.Result = destination;
            data.Success = true;
        }

        private async Task MoveFileAsync(FileData data, string source, string destination, bool overwrite, ExecutionContext context)
        {
            if (!File.Exists(source))
            {
                data.ErrorMessage = $"Source file not found: {source}";
                data.Success = false;
                return;
            }

            if (File.Exists(destination) && !overwrite)
            {
                data.ErrorMessage = $"Destination file already exists and overwrite is false: {destination}";
                data.Success = false;
                return;
            }

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Move(source, destination, overwrite);
            data.Result = destination;
            data.Success = true;
        }

        private async Task DeleteFileAsync(FileData data, string path, ExecutionContext context)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                data.ErrorMessage = $"File or directory not found: {path}";
                data.Success = false;
                return;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }

            data.Result = path;
            data.Success = true;
        }

        private async Task ListFilesAsync(FileData data, string directory, string filter, bool recursive, bool includeMetadata, ExecutionContext context)
        {
            if (!Directory.Exists(directory))
            {
                data.ErrorMessage = $"Directory not found: {directory}";
                data.Success = false;
                return;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directory, filter, searchOption);
            data.Files = files.ToList();

            if (includeMetadata)
            {
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var meta = new Dictionary<string, object>
                    {
                        ["FullName"] = fileInfo.FullName,
                        ["Name"] = fileInfo.Name,
                        ["Extension"] = fileInfo.Extension,
                        ["Length"] = fileInfo.Length,
                        ["CreationTime"] = fileInfo.CreationTime,
                        ["LastAccessTime"] = fileInfo.LastAccessTime,
                        ["LastWriteTime"] = fileInfo.LastWriteTime,
                        ["Attributes"] = fileInfo.Attributes.ToString()
                    };
                    data.Metadata.Add(meta);
                }
            }

            data.Result = string.Join(Environment.NewLine, files);
            data.Success = true;
        }

        private async Task CreateFolderAsync(FileData data, string path, ExecutionContext context)
        {
            if (Directory.Exists(path))
            {
                data.ErrorMessage = $"Directory already exists: {path}";
                data.Success = false;
                return;
            }

            Directory.CreateDirectory(path);
            data.Result = path;
            data.Success = true;
        }

        private async Task CreateFileAsync(FileData data, string path, string content, Encoding encoding, bool overwrite, ExecutionContext context)
        {
            if (File.Exists(path) && !overwrite)
            {
                data.ErrorMessage = $"File already exists and overwrite is false: {path}";
                data.Success = false;
                return;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, content, encoding, context.StopToken);
            data.Result = path;
            data.Success = true;
        }

        private async Task ReadFilesAsync(FileData data, string readMode, List<string> selectedFilePaths, string directory, string filter, bool recursive, Encoding encoding, bool includeMetadata, ExecutionContext context)
        {
            List<string> files = new List<string>();
            
            if (string.Equals(readMode, "Selected", StringComparison.OrdinalIgnoreCase) && selectedFilePaths != null && selectedFilePaths.Count > 0)
            {
                // Use selected file paths
                files = selectedFilePaths.Where(File.Exists).ToList();
            }
            else
            {
                // All files in directory matching filter
                if (!Directory.Exists(directory))
                {
                    data.ErrorMessage = $"Directory not found: {directory}";
                    data.Success = false;
                    return;
                }
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                files = Directory.GetFiles(directory, filter, searchOption).ToList();
            }

            var fileContents = new Dictionary<string, string>();
            var fileEntries = new List<object>();
            foreach (var file in files)
            {
                var fileContent = await File.ReadAllTextAsync(file, encoding, context.StopToken);
                fileContents[file] = fileContent;
                fileEntries.Add(new { path = file, content = fileContent });

                if (includeMetadata)
                {
                    var fileInfo = new FileInfo(file);
                    var meta = new Dictionary<string, object>
                    {
                        ["FullName"] = fileInfo.FullName,
                        ["Name"] = fileInfo.Name,
                        ["Extension"] = fileInfo.Extension,
                        ["Length"] = fileInfo.Length,
                        ["CreationTime"] = fileInfo.CreationTime,
                        ["LastAccessTime"] = fileInfo.LastAccessTime,
                        ["LastWriteTime"] = fileInfo.LastWriteTime,
                        ["Attributes"] = fileInfo.Attributes.ToString()
                    };
                    data.Metadata.Add(meta);
                }
            }

            data.Files = files;
            data.FileContents = fileContents;
            // Serialize array of objects to JSON for result
            data.Result = System.Text.Json.JsonSerializer.Serialize(fileEntries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            data.Success = true;
        }

        private async Task SelectFilesAsync(FileData data, string directory, string filter, bool recursive, bool includeMetadata, ExecutionContext context)
        {
            if (!Directory.Exists(directory))
            {
                data.ErrorMessage = $"Directory not found: {directory}";
                data.Success = false;
                return;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directory, filter, searchOption);
            data.Files = files.ToList();

            if (includeMetadata)
            {
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var meta = new Dictionary<string, object>
                    {
                        ["FullName"] = fileInfo.FullName,
                        ["Name"] = fileInfo.Name,
                        ["Extension"] = fileInfo.Extension,
                        ["Length"] = fileInfo.Length,
                        ["CreationTime"] = fileInfo.CreationTime,
                        ["LastAccessTime"] = fileInfo.LastAccessTime,
                        ["LastWriteTime"] = fileInfo.LastWriteTime,
                        ["Attributes"] = fileInfo.Attributes.ToString()
                    };
                    data.Metadata.Add(meta);
                }
            }

            data.Result = string.Join(Environment.NewLine, files);
            data.Success = true;
        }

        private Encoding GetEncoding(string encodingName)
        {
            return encodingName.ToLower() switch
            {
                "utf-8" => Encoding.UTF8,
                "ascii" => Encoding.ASCII,
                "utf-16" or "unicode" => Encoding.Unicode,
                "latin1" or "iso-8859-1" => Encoding.Latin1,
                _ => Encoding.UTF8
            };
        }

        private string GetSettingValue(string key, string fallback)
        {
            return Settings.TryGetValue(key, out var value) ? value : fallback;
        }


    }
}