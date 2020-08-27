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

		public bool connect() {
			string ip = setting.GetValueOrDefault("ip", null);

			if (ip == null) {
				Console.WriteLine("ip undefiend. Define as 'sfsc set ip [server_ip]'");
				return false;
			}

			string port = setting.GetValueOrDefault("port", null);

			if (port == null) {
				Console.WriteLine("port undefiend. Define as 'sfsc set port [server_port]'");
				return false;
			}

			int port_as_int;
			try {
				port_as_int = int.Parse(port);
			}
			catch(Exception) {
				Console.WriteLine("port isn't number. Redefine as 'sfsc set port [server_port]'");
				return false;
			}

			socketHelper.connect(ip, port_as_int);

			return true;
		}

		public void close() {
			socketHelper.close();
		}

		public void get(string path_source, string path_target) {

			FileStream stream = new FileInfo(path_target).Create();

			if (!connect()) 
				return;

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

			if (!connect())
				return;

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
			+ '\n' + "  sfsc set [key] [value]:"
			+ '\n' + "    key 'set' write key-value pair in local file"
			+ '\n' + "    INFO: program use 'ip' and 'port' or 'url' to connect"
			+ '\n'
			+ '\n' + "  sfsc dir [path]"
			+ '\n' + "    return 'dir' result with argument [path]"
			+ '\n'
			+ '\n' + "  sfsc get [source] [target_directory]"
			+ '\n' + "    get [source] (file or folder) from server to [target_directory]"
			+ '\n' + "      [source] will be placed as [target_directory]/[source]"
			+ '\n' + "    if [target_directory] not specified, [target_directory] - current directory"
			+ '\n'
			+ '\n' + "  sfsc send [source] [target_directory]"
			+ '\n' + "    send [source] (file or folder) to server placing in [target_directory]"
			+ '\n' + "      [source] will be placed as [target_directory]/[source]"
			+ '\n' + "    if [target_directory] not specified, [target_directory] - current server directory"
			+ '\n'
			+ '\n' + "  sfsc remove [target]"
			+ '\n' + "    remove [target] (file or folder) from server"
			+ '\n'
			;
			Console.WriteLine(help);

		}

		static string getLeftValue(string s) {
			return s.Substring(0, s.IndexOf(' '));
		}
		static string getRightValue(string s) {
			return s.Substring(s.IndexOf(' ') + 1);
		}

		static void Main(string[] args) {
			//TODO: blet try blet blet
			try {
				FileSyncClient scc = new FileSyncClient();
				if (!File.Exists("sfsc.config"))
					File.Create("sfsc.config").Close();
				string[] config_file = File.ReadAllLines("sfsc.config");
				foreach (string s in config_file) {
					scc.setting.Add(getLeftValue(s), getRightValue(s));
				}

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

						case "set":
							//todo: release
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
