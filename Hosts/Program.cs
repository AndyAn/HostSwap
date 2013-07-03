using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HostCore.Utils;
using HostCore.Components;
using Microsoft.Win32;

namespace Hosts
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFileManager manager = HostFileManager.GetInstance();
            HostFile host = null;

            switch (args.Length)
            {
                case 0:
                    ShowSettings();
                    break;
                case 1:
                    #region one parameter
                    switch (args[0].Trim().ToLower())
                    {
                        case "info":
                            ShowSettings();
                            break;
                        case "help":
                            ShowHelp();
                            break;
                        case "setenv":
                            SetSysEnvVariable();
                            ShowMessage("Environment path has been set.\n");
                            break;
                        case "flush":
                            manager.FlushDNS();
                            ShowMessage("Flush DNS completed.\n");
                            break;
                        case "list":
                            ShowList();
                            break;
                        case "reset":
                            manager.SwapHostFiles(new HostFile() { Path = manager.GlobalHostFile });
                            break;
                        default:
                            ShowError();
                            break;
                    }
                    break;
                    #endregion
                case 2:
                    #region two parameters
                    string param = args[1];

                    switch (args[0].Trim().ToLower())
                    {
                        case "info":
                            #region hosts info [all/sys]
                            if (param.Trim().ToLower() == "all")
                            {
                                ShowSettings(true);
                            }
                            else if (param.Trim().ToLower() == "sys")
                            {
                                ShowSettings(true, true);
                            }
                            else
                            {
                                ShowError();
                            }
                            break;
                            #endregion
                        case "change":
                            #region hosts change [host file name]
                            host = manager.GetHostFile(param);
                            ShowMessage(manager.SwapHostFiles(host) + "\n");
                            manager.FlushDNS();
                            break;
                            #endregion
                        case "new":
                            #region hosts new [host file name]
                            host = new HostFile();
                            host.Description = param;
                            ShowMessage("Please set <DisplayCharacter>:");
                            host.DisplayCharacter = Console.ReadLine().Trim();
                            ShowMessage("Would you like auto reset after ended the session of this hosts file? (Yes / No):");
                            host.PointsToLive = (Console.ReadLine().Trim().ToLower() == "yes");
                            manager.NewHostFile(host.Description, host.DisplayCharacter, host.PointsToLive, "");
                            ShowMessage(string.Format("New hosts file [{0}] created.\n", host.Description));
                            EditFile(manager.HostFileList.Last().FullPath);
                            break;
                            #endregion
                        case "edit":
                            #region hosts edit [host file name]
                            if (param.ToLower() == "global")
                            {
                                EditFile(manager.GlobalHostFile);
                            }
                            else if (param.ToLower() == "temp")
                            {
                                EditFile(manager.SystemHostFile);
                            }
                            else
                            {
                                host = manager.GetHostFile(param);
                                if (host != null)
                                {
                                    EditFile(host.FullPath);
                                }
                                else
                                {
                                    ShowMessage("The hosts file doesn't exist.\n");
                                }
                            }
                            break;
                            #endregion
                        case "delete":
                            #region hosts delete [host file name]
                            host = manager.GetHostFile(param);
                            if (host != null)
                            {
                                manager.DeleteHostFile(host.Description);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                            #endregion
                        case "remove":
                            #region hosts remove [host file name]
                            host = manager.GetHostFile(param);
                            if (host != null)
                            {
                                manager.RecycleHostFile(host.Description, true);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                            #endregion
                        case "restore":
                            #region hosts restore [host file name]
                            host = manager.GetHostFile(param);
                            if (host != null)
                            {
                                manager.RecycleHostFile(host.Description, false);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                            #endregion
                        default:
                            ShowError();
                            break;
                    }
                    break;
                    #endregion
                case 3:
                    #region three parameters
                    string oldName = args[1], newName = args[2];

                    switch (args[0].Trim().ToLower())
                    {
                        case "rename":
                            #region hosts rename [old host file name] [new host file name]
                            host = manager.GetHostFile(oldName);
                            string content = manager.GetHostContent(oldName);

                            if (host != null && content != manager.ErrorSign)
                            {
                                manager.EditHostFile(oldName, newName, host.DisplayCharacter, host.PointsToLive, content);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                            #endregion
                        default:
                            ShowError();
                            break;
                    }
                    break;
                    #endregion
                default:
                    ShowError();
                    break;
            }
        }

        private static void SetSysEnvVariable()
        {
            RegistryKey regLocalMachine = Registry.LocalMachine;
            RegistryKey regSYSTEM = regLocalMachine.OpenSubKey("SYSTEM", true);
            RegistryKey regControlSet001 = regSYSTEM.OpenSubKey("ControlSet001", true);
            RegistryKey regControl = regControlSet001.OpenSubKey("Control", true);
            RegistryKey regManager = regControl.OpenSubKey("Session Manager", true);
            RegistryKey regEnvironment = regManager.OpenSubKey("Environment", true);
            string[] paths = regEnvironment.GetValue("Path").ToString().TrimEnd(';').Split(';');
            string xotoolkit = paths.SingleOrDefault(p => p.TrimEnd('\\').ToLower() == HostFileManager.GetInstance().CurrentDirectory.ToLower());
            if (string.IsNullOrEmpty(xotoolkit))
            {
                regEnvironment.SetValue("Path", string.Format("{0};{1}", regEnvironment.GetValue("Path"), HostFileManager.GetInstance().CurrentDirectory));
            }
            regEnvironment.Close();
            regManager.Close();
            regControl.Close();
            regControlSet001.Close();
            regSYSTEM.Close();
            regLocalMachine.Close();

            Process cmd = new Process();
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            cmd.StartInfo.FileName = "CMD.exe";
            cmd.StartInfo.Arguments = "/C set PATH=C:";
            cmd.Start();
            cmd.Close();
        }

        private static void ShowSettings()
        {
            ShowSettings(false, false);
        }

        private static void ShowSettings(bool isDetail)
        {
            ShowSettings(isDetail, false);
        }

        private static void ShowSettings(bool isDetail, bool isSys)
        {
            try
            {
                string env = File.ReadAllLines(HostFileManager.GetInstance().SystemHostFile)[3].Split(' ')[1];
                HostFile envHost = HostFileManager.GetInstance().HostFileList.SingleOrDefault(host => host.Description.ToUpper() == env && !host.IsDelete);

                if (envHost == null)
                {
                    ShowMessage("Unknow hosts file.\n");
                }
                else
                {
                    ShowMessage(string.Format("\nCurrent hosts file is {0}.\n", envHost.Description));

                    if (isDetail)
                    {
                        ShowMessage(new string('=', 79));
                        if (isSys)
                        {
                            ShowMessage(string.Join("\n", File.ReadAllLines(HostFileManager.GetInstance().SystemHostFile).Where(l => !l.StartsWith("#") && l.Trim().Length > 0).ToArray()) + "\n");
                        }
                        else
                        {
                            ShowMessage(envHost.Content + "\n");
                        }
                    }
                }
            }
            catch
            {
                ShowMessage("Unknow hosts file.\n");
            }
        }

        private static void EditFile(string path)
        {
            Process.Start("notepad", path);
        }

        private static void ShowList()
        {
            List<HostFile> list = HostFileManager.GetInstance().HostFileList;

            ShowMessage("\n\tHosts File Name     \tTrayIcon  \tMark Delete");
            ShowMessage("\t===================================================");
            foreach (HostFile file in list)
            {
                ShowMessage(string.Format("\t{0}\t{1}\t{2}", file.Description.PadRight(20), file.DisplayCharacter.PadRight(10), (file.IsDelete ? "[delete]" : "")));
            }
            ShowMessage("");
        }

        private static void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        private static void ShowError()
        {
            ShowMessage("Invalid parameters. Please use [Help] command to see the usage.\n");
        }

        private static void ShowHelp()
        {
            ShowMessage(string.Format("\n{0} [Version {1}]", AssemblyInfo.Title, AssemblyInfo.Version));
            ShowMessage(string.Format("{0}  {1} {2}.\n", AssemblyInfo.Copyright, AssemblyInfo.Company, AssemblyInfo.Trademark));
            ShowMessage("Help on Usage:");
            ShowMessage("    hosts [operation] [<param>]\n");
            ShowMessage("    Operation:");
            ShowMessage("        change\tSwap different hosts files with [<param> hosts file name].");
            ShowMessage("        delete\tDelete a hosts file with [<param> hosts file name].");
            ShowMessage("        edit\tEdit a hosts file with [<param> hosts file name]. If <param>");
            ShowMessage("            \tis Global, it will edit Global hosts file; If <Param> is.");
            ShowMessage("            \tTemp, that means the system hosts file will be edited.");
            ShowMessage("        flush\tFlush DNS.");
            ShowMessage("        help\tGet help on this command.");
            ShowMessage("        info\tShow current hosts file name. With [<param> all/sys], will show");
            ShowMessage("            \tthe detailed settings.");
            ShowMessage("        list\tList all hosts files");
            ShowMessage("        new\tCreate a new hosts file with a name [<param> hosts file name].");
            ShowMessage("        remove\tMark a hosts file as removed, cannot be used with [change]");
            ShowMessage("              \toperation.");
            ShowMessage("        rename\tRename a hosts file name.");
            ShowMessage("        reset\tReset system hosts file, remove all customized settings but");
            ShowMessage("             \tglobal one.");
            ShowMessage("        restore\tMark a hosts file as available.\n");
            ShowMessage("        setenv\tSet system environment variable, so that can use this command");
            ShowMessage("              \tanywhere without path (Require Administrator privileges).");
            ShowMessage("    Sample:");
            ShowMessage("        hosts change gzdev.\n");
        }
    }
}
