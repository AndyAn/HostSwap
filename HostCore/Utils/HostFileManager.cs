using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HostCore.Components;
using System.IO;
using System.Xml;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;

namespace HostCore.Utils
{
    public class HostFileManager
    {
        #region Private Fields

        private static readonly HostFileManager hostManager = new HostFileManager();

        #endregion

        #region Constructor

        private HostFileManager()
        {
            CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');//Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            HostsPath = @"HostFiles";
            SystemHostFile = Path.Combine(Environment.GetEnvironmentVariable(@"SystemRoot"), @"system32\drivers\etc\hosts");
            GlobalHostFile = Path.Combine(Path.Combine(CurrentDirectory, HostsPath), @"HostEntries.host");
            HostFileConfigFile = Path.Combine(CurrentDirectory, @"Hosts.cfg");
            IsHostFilePointToLive = false;
            ErrorSign = "#N/A#";

            if (!Directory.Exists(Path.Combine(CurrentDirectory, HostsPath)))
            {
                Directory.CreateDirectory(Path.Combine(CurrentDirectory, HostsPath));
            }

            if (!File.Exists(GlobalHostFile))
            {
                File.WriteAllText(GlobalHostFile, "");
            }

            if (!File.Exists(HostFileConfigFile))
            {
                ObjectSerializer<List<HostFile>>.Save(new List<HostFile>(), HostFileConfigFile);
            }
        }

        #endregion

        #region Public Properties

        private List<HostFile> hostFiles = null;
        public List<HostFile> HostFileList
        {
            get
            {
                if (hostFiles == null)
                {
                    hostFiles = ObjectSerializer<List<HostFile>>.Load(HostFileConfigFile);

                    foreach (HostFile host in hostFiles)
                    {
                        host.FullPath = Path.Combine(CurrentDirectory, host.Path);
                        host.Content = File.ReadAllText(host.FullPath);
                    }
                }

                return hostFiles;
            }
        }

        public bool IsHostFilePointToLive { get; private set; }

        public string SystemHostFile { get; private set; }

        public string GlobalHostFile { get; private set; }

        public string HostFileConfigFile { get; private set; }

        public string HostsPath { get; private set; }

        public string CurrentDirectory { get; private set; }

        public string ErrorSign { get; private set; }

        #endregion

        #region Public Methods

        public static HostFileManager GetInstance()
        {
            return hostManager;
        }

        public void NewHostFile(string name, string displayingChar, bool pointToLive, string content)
        {
            HostFile host = new HostFile();

            host.Description = name;
            host.DisplayCharacter = displayingChar;
            host.Path = Path.Combine(HostsPath, name + ".host");
            host.PointsToLive = pointToLive;
            host.IsDelete = false;
            host.FullPath = Path.Combine(CurrentDirectory, host.Path);
            host.Content = content;

            File.WriteAllText(Path.Combine(CurrentDirectory, host.Path), content);

            HostFileList.Add(host);

            SaveHostFilesToFile();
        }

        public void EditHostFile(string oldName, string newName, string displayingChar, bool pointToLive, string content)
        {
            if (newName != "Global Host File" && oldName != "Global Host File" 
                && newName != "System Host File" && oldName != "System Host File")
            {
                HostFile host = GetHostFile(oldName);

                if (host != null)
                {
                    int index = HostFileList.IndexOf(host);
                    string oldFile = host.FullPath;
                    string newFile = Path.Combine(CurrentDirectory, HostsPath, newName + ".host");

                    host.Description = newName;
                    host.Path = Path.Combine(HostsPath, newName + ".host");
                    host.FullPath = Path.Combine(CurrentDirectory, host.Path);
                    host.Content = content;
                    if (displayingChar.Length > 0)
                    {
                        host.DisplayCharacter = displayingChar;
                        host.PointsToLive = pointToLive;
                    }

                    HostFileList[index] = host;
                    if (File.Exists(Path.Combine(CurrentDirectory, HostsPath, oldName + ".host")))
                    {
                        if (oldName != newName)
                        {
                            File.Copy(oldFile, newFile, true);
                            File.Delete(oldFile);
                        }

                        File.WriteAllText(newFile, content);
                    }
                    else
                    {
                        HostFileList.RemoveAt(index);
                    }

                    SaveHostFilesToFile();
                }
            }
            else
            {
                File.WriteAllText((newName.StartsWith("System") ? SystemHostFile : GlobalHostFile), content);
            }
        }

        public void DeleteHostFile(string name)
        {
            List<HostFile> hosts = HostFileList.Where(h => h.Description == name && h.IsDelete).ToList();

            if (hosts.Count > 0)
            {
                int index = HostFileList.IndexOf(hosts[0]);
                string path = hosts[0].FullPath;

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                HostFileList.RemoveAt(index);

                SaveHostFilesToFile();
            }
        }

        public void RecycleHostFile(string name, bool recycle)
        {
            List<HostFile> hosts = HostFileList.Where(h => h.Description == name && h.IsDelete != recycle).ToList();

            if (hosts.Count > 0)
            {
                hosts[0].IsDelete = recycle;

                SaveHostFilesToFile();
            }
        }

        public string SwapHostFiles(HostFile hostFile)
        {
            string path = Path.Combine(CurrentDirectory, hostFile.Path);

            if (hostFile == null)
            {
                return "Hosts file entry not found";
            }

            if (!File.Exists(path))
            {
                return "Hosts file doesn't exist. (" + path + ")";
            }

            if (hostFile.IsDelete)
            {
                return "Hosts file is marked as deleted.";
            }

            try
            {
                //open the windows host file
                FileStream targetFile = null;
                StreamWriter sw = null;
                try
                {
                    targetFile = new FileStream(SystemHostFile, FileMode.Create, FileAccess.Write);
                    sw = new StreamWriter(targetFile);

                    //Comment
                    sw.WriteLine("##################################################################");
                    sw.WriteLine("# This file was automatically generated by HostSwap - HostCore.dll");
                    sw.WriteLine("# Date: " + DateTime.Now.ToString());
                    sw.WriteLine("# " + hostFile.Description.ToUpper() + " Environment");
                    sw.WriteLine("##################################################################");

                    sw.WriteLine();
                    sw.WriteLine();

                    //add the static host entries if possible
                    if (File.Exists(GlobalHostFile))
                    {
                        FileStream sourceFile = null;
                        StreamReader sr = null;
                        try
                        {
                            //get the additional host entries
                            sourceFile = new FileStream(GlobalHostFile, FileMode.Open, FileAccess.Read);
                            sr = new StreamReader(sourceFile);
                            sw.Write(sr.ReadToEnd());
                        }
                        finally
                        {
                            if (sr != null)
                                sr.Close();
                            if (sourceFile != null)
                                sourceFile.Close();
                        }
                    }

                    //add the environmental host entries if possible
                    if (path != GlobalHostFile)
                    {
                        FileStream sourceFile = null;
                        StreamReader sr = null;
                        try
                        {
                            //get the additional host entries
                            sourceFile = new FileStream(path, FileMode.Open, FileAccess.Read);
                            sr = new StreamReader(sourceFile);
                            sw.WriteLine();
                            sw.WriteLine();
                            sw.WriteLine("##################################################################");
                            sw.WriteLine("# " + hostFile.Description);
                            sw.WriteLine("##################################################################");
                            sw.Write(sr.ReadToEnd());
                        }
                        finally
                        {
                            if (sr != null)
                                sr.Close();
                            if (sourceFile != null)
                                sourceFile.Close();
                        }
                    }

                    if (hostFile.Path == GlobalHostFile)
                    {
                        return "Hosts file has been reseted.";
                    }
                    else
                    {
                        return "New hosts settings accepted.";
                    }
                }
                finally
                {
                    if (sw != null)
                        sw.Close();
                    if (targetFile != null)
                        targetFile.Close();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                IsHostFilePointToLive = hostFile.PointsToLive;
            }
        }

        public void FlushDNS()
        {
            try
            {
                Process cmd = new Process();
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                cmd.StartInfo.FileName = "CMD.exe";
                cmd.StartInfo.Arguments = "/C ipconfig /flushdns";
                cmd.Start();
                cmd.Close();
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public HostFile GetHostFile(string name)
        {
            HostFile host = HostFileList.SingleOrDefault(h => h.Description.ToLower() == name.ToLower());

            return host;
        }

        public string GetHostContent(string name)
        {
            HostFile host = GetHostFile(name);

            if (host == null)
            {
                return ErrorSign;
            }
            else
            {
                return host.Content;
            }
        }

        /// <summary>
        /// Import Host Files
        /// </summary>
        /// <param name="files">Host File Packages</param>
        public void ImportHostFiles(string[] files)
        {
            try
            {
                string hostPath = string.Empty;
                foreach (string file in files)
                {
                    string unZipFiles = UnZipPackage(file);
                    string[] hosts = unZipFiles.Split('|');
                    foreach (string host in hosts)
                    {
                        string[] cfg = host.Split('~');
                        hostPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cfg[2]);

                        if (File.Exists(hostPath))
                        {
                            HostFile hostFileTemp = this.HostFileList.SingleOrDefault(h => h.Path == cfg[2]);
                            if (hostFileTemp == null)
                            {
                                HostFile hostFile = new HostFile();
                                hostFile.Description = cfg[0];
                                hostFile.DisplayCharacter = (cfg[1].Trim() == string.Empty ? "?" : cfg[1].Trim());
                                hostFile.Path = cfg[2];
                                hostFile.PointsToLive = (cfg[3].Trim() == string.Empty ? false : Convert.ToBoolean(cfg[3].Trim()));
                                hostFile.IsDelete = false;

                                this.HostFileList.Add(hostFile);
                            }
                        }

                        using (StreamWriter sw = new StreamWriter(hostPath, false))
                        {
                            sw.Write(Encoding.UTF8.GetString(Convert.FromBase64String(cfg[4])));
                        }
                    }
                }

                this.SaveHostFilesToFile();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void ExportHostFiles(string[] hosts, string file)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (string host in hosts)
                {
                    HostFile hostFile = this.HostFileList.SingleOrDefault(h => h.Description == host);
                    if (hostFile != null)
                    {
                        sb.Append(hostFile.Description + "~" + hostFile.DisplayCharacter + "~" + hostFile.Path + "~" + hostFile.PointsToLive.ToString());
                        using (StreamReader sr = new StreamReader(Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, hostFile.Path)))
                        {
                            sb.Append("~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(sr.ReadToEnd())) + "|");
                        }
                    }
                }

                using (FileStream fs = new FileStream(file, FileMode.Create))
                {
                    byte[] pak = ZipPackage(sb.ToString().TrimEnd('|'));
                    fs.Write(pak, 0, pak.Length);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion

        #region Private Methods

        private void SaveHostFilesToFile()
        {
            ObjectSerializer<List<HostFile>>.Save(HostFileList, HostFileConfigFile);
            hostFiles = ObjectSerializer<List<HostFile>>.Load(HostFileConfigFile);
        }

        private byte[] ZipPackage(string package)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(package);
            MemoryStream ms = new MemoryStream();
            using (GZipStream zip = new GZipStream(ms, CompressionMode.Compress, true))
            {
                zip.Write(buffer, 0, buffer.Length);
            }

            ms.Position = 0;
            MemoryStream outStream = new MemoryStream();

            byte[] compressed = new byte[ms.Length];
            ms.Read(compressed, 0, compressed.Length);

            byte[] gzBuffer = new byte[compressed.Length + 4];
            System.Buffer.BlockCopy(compressed, 0, gzBuffer, 4, compressed.Length);
            System.Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gzBuffer, 0, 4);
            return gzBuffer;
        }

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="file">Host File Package</param>
        /// <returns>Content of package</returns>
        private string UnZipPackage(string file)
        {
            byte[] src;
            using (FileStream fs = new FileStream(file, FileMode.Open))
            {
                src = new byte[(int)fs.Length];
                fs.Read(src, 0, src.Length);
            }

            using (MemoryStream ms = new MemoryStream())
            {
                int msgLength = BitConverter.ToInt32(src, 0);
                ms.Write(src, 4, src.Length - 4);

                byte[] buffer = new byte[msgLength];

                ms.Position = 0;
                using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    zip.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }

        #endregion
    }
}
