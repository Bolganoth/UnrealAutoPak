using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace UnrealAutoPak {
    internal class Program {
        public static string MakeRelativePath(string fromPath, string toPath) {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException(nameof(fromPath));
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException(nameof(toPath));

            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) {
                // 不是同一种路径，无法转换成相对路径。
                return toPath;
            }

            if (fromUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase)
                && !fromPath.EndsWith("/", StringComparison.OrdinalIgnoreCase)
                && !fromPath.EndsWith("\\", StringComparison.OrdinalIgnoreCase)) {
                // 如果是文件系统，则视来源路径为文件夹。
                fromUri = new Uri(fromPath + Path.DirectorySeparatorChar);
            }

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase)) {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        public static void ExecuteCmdCommand(List<string> commands) {
            var p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.UseShellExecute = false; //是否使用操作系统shell启动
            p.StartInfo.RedirectStandardInput = true; //接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true; //由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true; //重定向标准错误输出
            p.StartInfo.CreateNoWindow = true; //不显示程序窗口
            p.Start(); //启动程序

            //向cmd窗口发送输入信息
            foreach (var cmd in commands) {
                p.StandardInput.WriteLine(cmd);
            }

            p.StandardInput.WriteLine("exit");

            p.StandardInput.AutoFlush = true;
            //向标准输入写入要执行的命令。这里使用&是批处理命令的符号，表示前面一个命令不管是否执行成功都执行后面(exit)命令，如果不执行exit命令，后面调用ReadToEnd()方法会假死
            //同类的符号还有&&和||前者表示必须前一个命令执行成功才会执行后面的命令，后者表示必须前一个命令执行失败才会执行后面的命令

            //获取cmd窗口的输出信息
            var output = p.StandardOutput.ReadToEnd();

            p.WaitForExit(); //等待程序执行完退出进程
            p.Close();

            Console.WriteLine(output);
        }

        public static void Main(string[] args) {
            string folderPath;
            var currentDirectory = Path.GetDirectoryName(Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location));
            try {
                folderPath = args[0];
            }
            catch (Exception) {
                Console.Write("请输入文件夹名称：");
                var input = Console.ReadLine();
                folderPath = Path.IsPathRooted(input) ? input : Path.Combine(currentDirectory!, input ?? "?");
            }

            // 检查文件夹是否存在
            if (!Directory.Exists(folderPath)) {
                Console.WriteLine("文件夹不存在！");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return;
            }

            var parentPath = Directory.GetParent(folderPath)?.FullName;
            var folderName = Path.GetFileName(folderPath);

            // 获取文件夹中的所有文件
            string[] fileList = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            string[] relativeFileList = Array.Empty<string>();
            Console.WriteLine("找到文件：");

            // 输出相对路径
            foreach (string file in fileList) {
                // 获取文件的相对路径
                string relativePath = MakeRelativePath(parentPath, file).Replace("\\", "/");
                Console.WriteLine(relativePath);
                relativeFileList = relativeFileList.Append("../../../" + relativePath).ToArray();
            }

            Console.WriteLine("\n");

            var packFileName = "packFiles.txt";
            var outputFilePath = Path.Combine(parentPath!, packFileName);
            File.WriteAllLines(outputFilePath, relativeFileList);

            //建立符号链接
            List<string> makeLinkCommands = new List<string> { $"mklink /D \"{currentDirectory}\\{folderName}\" \"{folderPath}\"" };
            ExecuteCmdCommand(makeLinkCommands);

            try {
                var packProcess = Process.Start($"{currentDirectory}\\UnrealPak\\2\\3\\UnrealPak.exe", $"\"{folderPath}.pak\" -Create=\"{parentPath}\\{packFileName}\" -compressd");
                if (packProcess == null) throw new Win32Exception();
                packProcess.WaitForExit();
            }
            catch (Win32Exception) {
                Console.WriteLine($"无法找到{currentDirectory}\\UnrealPak\\2\\3\\UnrealPak.exe！");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }

            //删除符号链接
            List<string> removeLinkCommands = new List<string> { $"rmdir \"{currentDirectory}\\{folderName}\"" };
            ExecuteCmdCommand(removeLinkCommands);
            File.Delete(outputFilePath);
        }
    }
}