using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace dir2tree
{
    public partial class MainWindow : Window
    {
        private const int MaxFilesToShow = 20;

        
        private readonly HashSet<string> ignoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // IDE و Editor
            ".idea",
            ".vscode",
            ".vs",
            
            // Python
            ".venv",
            "venv",
            "__pycache__",
            "*.pyc",
            "*.pyo",
            "*.egg-info",
            
            // Node.js
            "node_modules",
            ".next",
            
            // .NET
            "bin",
            "obj",
            "packages",
            
            // Git
            ".git",
            ".gitignore",
            
            // Build و Cache
            "dist",
            "build",
            ".cache",
            
            // OS specific
            ".DS_Store",
            "Thumbs.db",
            "desktop.ini",
            
            // Other
            "*.log"
        };

        public MainWindow()
        {
            InitializeComponent();
            
            // Load the last directory path if it exists
            string lastPath = Properties.Settings.Default.LastDirectoryPath;
            if (!string.IsNullOrEmpty(lastPath) && Directory.Exists(lastPath))
            {
                txtPath.Text = lastPath;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save the current path when the window is closing
            if (!string.IsNullOrEmpty(txtPath.Text) && Directory.Exists(txtPath.Text))
            {
                Properties.Settings.Default.LastDirectoryPath = txtPath.Text;
                Properties.Settings.Default.Save();
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder to generate tree structure";
                dialog.ShowNewFolderButton = false;

                // If txtPath contains a valid existing directory, open the dialog at that path
                string currentPath = (txtPath.Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
                {
                    try
                    {
                        dialog.SelectedPath = currentPath;
                    }
                    catch
                    {
                        // Ignore any exception and let the dialog use its default location
                    }
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtPath.Text = dialog.SelectedPath;
                    
                    // Save the selected path to settings
                    Properties.Settings.Default.LastDirectoryPath = dialog.SelectedPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string path = txtPath.Text.Trim();

            if (string.IsNullOrEmpty(path))
            {
                System.Windows.MessageBox.Show("Please enter or select a directory path.",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("The specified directory does not exist.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                StringBuilder output = new StringBuilder();
                DirectoryInfo rootDir = new DirectoryInfo(path);

                // Add root directory name
                output.AppendLine(rootDir.Name + "/");

                // Generate tree
                GenerateTree(rootDir, "", output, true);

                txtOutput.Text = output.ToString();
                
                // Save the path to settings if it's different from the current saved path
                if (Properties.Settings.Default.LastDirectoryPath != path)
                {
                    Properties.Settings.Default.LastDirectoryPath = path;
                    Properties.Settings.Default.Save();
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Windows.MessageBox.Show("Access denied to some directories.",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtOutput.Text))
            {
                try
                {
                    System.Windows.Clipboard.SetText(txtOutput.Text);
                    System.Windows.MessageBox.Show("Tree structure copied to clipboard!",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to copy: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Nothing to copy. Generate a tree first.",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        
        private bool ShouldIgnore(string name)
        {
            // چک مستقیم نام
            if (ignoredNames.Contains(name))
                return true;

            // چک pattern ها (مثل *.pyc)
            foreach (var pattern in ignoredNames)
            {
                if (pattern.Contains("*"))
                {
                    string regexPattern = "^" + pattern.Replace(".", "\\.").Replace("*", ".*") + "$";
                    if (System.Text.RegularExpressions.Regex.IsMatch(name, regexPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private void GenerateTree(DirectoryInfo directory, string indent,
            StringBuilder output, bool isLast)
        {
            try
            {
                // Get subdirectories and files - با فیلتر ignore
                var directories = directory.GetDirectories()
                    .Where(d => !ShouldIgnore(d.Name))
                    .OrderBy(d => d.Name)
                    .ToArray();

                var files = directory.GetFiles()
                    .Where(f => !ShouldIgnore(f.Name))
                    .OrderBy(f => f.Name)
                    .ToArray();

                int totalItems = directories.Length + (files.Length > MaxFilesToShow ? 1 : files.Length);
                int currentItem = 0;

                // Process subdirectories
                foreach (var dir in directories)
                {
                    currentItem++;
                    bool isLastItem = (currentItem == totalItems);

                    string connector = isLastItem ? "└── " : "├── ";
                    output.AppendLine(indent + connector + dir.Name + "/");

                    string newIndent = indent + (isLastItem ? "    " : "│   ");
                    GenerateTree(dir, newIndent, output, isLastItem);
                }

                // Process files
                if (files.Length > MaxFilesToShow)
                {
                    // Show summary instead of individual files
                    var fileGroups = files
                        .GroupBy(f => f.Extension.ToLower())
                        .OrderByDescending(g => g.Count());

                    currentItem++;
                    bool isLastItem = (currentItem == totalItems);
                    string connector = isLastItem ? "└── " : "├── ";

                    StringBuilder summary = new StringBuilder();
                    summary.Append($"[{files.Length} files: ");

                    var groupList = new List<string>();
                    foreach (var group in fileGroups)
                    {
                        string ext = string.IsNullOrEmpty(group.Key) ? "no extension" : group.Key;
                        groupList.Add($"{group.Count()} {ext}");
                    }

                    summary.Append(string.Join(", ", groupList));
                    summary.Append("]");

                    output.AppendLine(indent + connector + summary.ToString());
                }
                else
                {
                    // Show individual files
                    foreach (var file in files)
                    {
                        currentItem++;
                        bool isLastItem = (currentItem == totalItems);
                        string connector = isLastItem ? "└── " : "├── ";

                        output.AppendLine(indent + connector + file.Name);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have access to
                output.AppendLine(indent + "[Access Denied]");
            }
        }
    }
}



