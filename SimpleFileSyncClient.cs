using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using Simple;

namespace Simple.FileSyncClient {
	public class FileSyncClient {
		public Dictionary<string, string> setting = new Dictionary<string, string>();

		public SocketHelper socketHelper = new SocketHelper();

		public void connect() {
			string ip = setting.GetValueOrDefault("ip", null);
			string port = setting.GetValueOrDefault("port", null);
			//TODO: check for null
			//TOOD: check for parse
			socketHelper.connect(ip, int.Parse(port));
			//
		}

		public void close() {
			socketHelper.close();
		}

		public void get(string path_source, string path_target) {

			FileStream stream = new FileInfo(path_target).Create();

			connect();

			socketHelper.sendMessage("get");

			socketHelper.sendMessage(path_source);

			byte[] file = socketHelper.receiveBytesMessage();

			stream.Write(file, 0, file.Length);
			stream.Close();

			close();
		}

		public string replaceToDirectorySeparator(string path) { 
			return Path.DirectorySeparatorChar == '/' ? path.Replace('\\', '/') : path.Replace('/', '\\');
		}

		public string getRelativePath(string relative_from, string full_path) {
			//Console.WriteLine(relative_from + "|" + full_path);
			if (full_path.Contains(relative_from)) {
				if (!Path.EndsInDirectorySeparator(relative_from))
					relative_from += Path.DirectorySeparatorChar;
				return full_path.Substring(full_path.LastIndexOf(relative_from) + relative_from.Length);
			}
			else {
				Console.WriteLine("getRelativePath error");
				return null;
			}
				
		}

		public struct RelativeFileName {
			public string real;
			public string relative;
			public RelativeFileName(string real, string relative) {
				this.real = real;
				this.relative = relative;
			}
		}

		public IEnumerable<RelativeFileName> getAllFiles(string source, string current) {
			if (File.Exists(current)) {
				if (current.EndsWith(source))
					yield return new RelativeFileName(current, Path.GetFileName(current));
				else
					yield return new RelativeFileName(current, getRelativePath(source, Path.GetFullPath(current)));
			}
			else {
				DirectoryInfo directoryInfo = new DirectoryInfo(current);
				foreach (var file in directoryInfo.GetFiles()) {
					yield return new RelativeFileName(file.FullName, getRelativePath(source, file.FullName));
				}
				foreach (var directory in directoryInfo.GetDirectories()) {
					foreach (var bytes in getAllFiles(source, directory.FullName)) {
						yield return bytes;
					}
				}
			}
		}

		public IEnumerable<RelativeFileName> getAllFilesRelativeRecursivity(string source, string current) {
			if (File.Exists(current)) {
				yield return new RelativeFileName(current, getRelativePath(source, Path.GetFullPath(current)));
			}
			else {
				DirectoryInfo directoryInfo = new DirectoryInfo(current);
				foreach (var file in directoryInfo.GetFiles()) {
					yield return new RelativeFileName(file.FullName, getRelativePath(source, file.FullName));
				}
				foreach (var directory in directoryInfo.GetDirectories()) {
					foreach (var relativeFileName in getAllFilesRelativeRecursivity(source, directory.FullName)) {
						yield return relativeFileName;
					}
				}
			}
		}

		public IEnumerable<RelativeFileName> AllFilesRelative(string source) {
			if (File.Exists(source)) {
				yield return new RelativeFileName(Path.GetFullPath(source), Path.GetFileName(source));
			}
			else {
				foreach (var relativeFileName in getAllFilesRelativeRecursivity(source, source)) {
					yield return relativeFileName;
				}
			}
		}

		public void send(string path_source, string path_target) {
			
			connect();
			
			socketHelper.sendMessage("send");

			socketHelper.sendMessage(path_target);
			
			foreach (var relativeFileName in getAllFiles(path_source, path_source)) {
				Console.WriteLine(relativeFileName.relative);
				
				socketHelper.sendMessage("continue");
				socketHelper.sendMessage(relativeFileName.relative);

				string answer = socketHelper.receiveMessage();
				if (answer == "ok") {
					
					socketHelper.sendBytesMessage(File.ReadAllBytes(relativeFileName.real));
				}
				else {
					Console.WriteLine(answer);
				}
			}
			
			socketHelper.sendMessage("break");

			close();
			
		}
	}

	class Program {

		static void showHelp() {

			string help =
			         "Usage:"
			+ '\n' + "  scc set [key] [value]:"
			+ '\n' + "    key 'set' write key-value pair in local file"
			+ '\n' + "    INFO: program use 'ip' and 'port' or 'url' to connect"
			+ '\n'
			+ '\n' + "  scc dir [path]"
			+ '\n' + "    return 'dir' result with argument [path]"
			+ '\n'
			+ '\n' + "  scc get [source] [target]"
			+ '\n' + "    get [source] (file or folder) from server to [target]"
			+ '\n' + "    if [target] not specified, [target] - current directory"
			+ '\n' + "    type 'help get' to more info"
			+ '\n'
			+ '\n' + "  scc send [source] [target]"
			+ '\n' + "    send [source] (file or folder) to server placing in [target]"
			+ '\n' + "    if [target] not specified, [target] - current server directory"
			+ '\n' + "    type 'help send' to more info"
			+ '\n'
			+ '\n' + "  scc remove [target]"
			+ '\n' + "    remove [target] (file or folder) from server"
			+ '\n'
			;
			Console.WriteLine(help);

		}

		static void showHelpGet() {
			string help =
			         "Usage:"
			+ '\n' + "scc get [source] [target]"
			+ '\n'
			+ '\n' + "  if [source] == file and [target] == file"
			+ '\n' + "    then [source] will be placed as [target]"
			+ '\n' + "    example: scc get A.txt B.txt"
			+ '\n' + "    we get file A, but place it as B"
			+ '\n' + "    example: scc get A.txt Folder/B.txt"
			+ '\n' + "    we get file A, but place it as B in Folder/"
			+ '\n'
			+ '\n' + "  if [source] == file and [target] == folder"
			+ '\n' + "    then [source] will be placed in [target]"
			+ '\n' + "    example: scc get A.txt Folder/"
			+ '\n' + "    we get file A, but place it as Folder/A"
			+ '\n'
			+ '\n' + "  if [source] == folder and [target] == file"
			+ '\n' + "    then the program says it's WRONG situation"
			+ '\n' + "    and what should happen when you get folder as file?"
			+ '\n'
			+ '\n' + "  if [source] == folder and [target] == folder"
			+ '\n' + "    then [source] will be placed as [target]"
			+ '\n' + "    example: scc get Their/ My/"
			+ '\n' + "    we get folder Their, but place it as My"
			+ '\n'
			;
			Console.WriteLine(help);
		}

		static void showHelpSend() {
			string help =
					 "Usage:"
			+ '\n' + "scc send [source] [target]"
			+ '\n'
			+ '\n' + "  if [source] == file and [target] == file"
			+ '\n' + "    then [source] will be placed as [target]"
			+ '\n' + "    example: scc send A.txt B.txt"
			+ '\n' + "    we send file A, but on server it will be placed as B"
			+ '\n' + "    example: scc send A.txt Folder/B.txt"
			+ '\n' + "    we send file A, but on server it will be placed as B in Folder/"
			+ '\n'
			+ '\n' + "  if [source] == file and [target] == folder"
			+ '\n' + "    then [source] will be placed in [target]"
			+ '\n' + "    example: scc send A.txt Folder/"
			+ '\n' + "    we send file A, but on server it will be placed in Folder/"
			+ '\n'
			+ '\n' + "  if [source] == folder and [target] == file"
			+ '\n' + "    then the program says it's WRONG situation"
			+ '\n' + "    and what should happen when you send folder as file?"
			+ '\n'
			+ '\n' + "  if [source] == folder and [target] == folder"
			+ '\n' + "    then [source] will be placed as [target]"
			+ '\n' + "    example: scc send Their/ My/"
			+ '\n' + "    we send folder Their, but on server it will be placed as My"
			+ '\n'
			;
			Console.WriteLine(help);
		}

		static void Main(string[] args) {
			//TODO: blet try blet blet
			try {
				FileSyncClient scc = new FileSyncClient();
				//TODO: read from file
				scc.setting.Add("ip", "127.0.0.1");
				scc.setting.Add("port", "11211");
				//

				//TODO: always check args[1] and args[2]
				if (args.Length > 0) {
					switch (args[0]) {
						case "get":
						scc.get(args[1], args[2]);
						break;
						case "send":
						scc.send(args[1], args[2]);
						break;
						case "help":
						if (args.Length > 1) {
							switch (args[1]) {
								case "get":
								showHelpGet();
								break;
								case "send":
								showHelpSend();
								break;
								default:
								Console.WriteLine("Help page not found");
								break;
							}
							return;
						}
						goto case "--help";
						case "-h":
						case "--h":
						case "-help":
						case "--help":
						showHelp();
						break;
						default:
						break;
					}
				}
				else
					showHelp();
			}
			catch (Exception e) {
				Console.WriteLine(e.Message + "\nStackTrace:\n" + e.StackTrace);
			}
		}
	}
}
