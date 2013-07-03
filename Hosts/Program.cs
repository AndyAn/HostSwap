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

            switch (args.Length)
            {
                case 0:
                    ShowSettings();
                    break;
                case 1:
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
                case 2:
                    string param = args[1];
                    HostFile host = null;

                    switch (args[0].Trim().ToLower())
                    {
                        case "info":
                            if (param.Trim().ToLower() == "all")
                            {
                                ShowSettings(true);
                            }
                            else
                            {
                                ShowError();
                            }
                            break;
                        case "change":
                            host = manager.HostFileList.SingleOrDefault(h => h.Description.ToLower() == param.ToLower());
                            manager.FlushDNS();
                            ShowMessage(manager.SwapHostFiles(host) + "\n");
                            break;
                        case "new":
                            host = new HostFile();
                            host.Description = param;
                            ShowMessage("Please set <DisplayCharacter>:");
                            host.DisplayCharacter = Console.ReadLine().Trim();
                            ShowMessage("Would you like auto reset after ended the session of this hosts file? (Yes / No):");
                            host.PointsToLive = (Console.ReadLine().Trim().ToLower() == "yes");
                            manager.NewHostFile(host.Description, host.DisplayCharacter, host.PointsToLive, "");
                            ShowMessage(string.Format("New hosts file [{0}] created.\n", host.Description));
                            EditFile(manager.HostFileList.Last().Path);
                            break;
                        case "edit":
                            if (param.ToLower() == "global")
                            {
                                EditFile(HostFileManager.GetInstance().GlobalHostFile);
                            }
                            else if (param.ToLower() == "temp")
                            {
                                EditFile(HostFileManager.GetInstance().SystemHostFile);
                            }
                            else
                            {
                                host = manager.HostFileList.SingleOrDefault(h => h.Description.ToLower() == param.ToLower());
                                if (host != null)
                                {
                                    EditFile(Path.Combine(HostFileManager.GetInstance().CurrentDirectory, host.Path));
                                }
                                else
                                {
                                    ShowMessage("The hosts file doesn't exist.\n");
                                }
                            }
                            break;
                        case "delete":
                            host = manager.HostFileList.SingleOrDefault(h => h.Description.ToLower() == param.ToLower());
                            if (host != null)
                            {
                                manager.DeleteHostFile(host.Description);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                        case "remove":
                            host = manager.HostFileList.SingleOrDefault(h => h.Description.ToLower() == param.ToLower());
                            if (host != null)
                            {
                                manager.RecycleHostFile(host.Description, true);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                        case "restore":
                            host = manager.HostFileList.SingleOrDefault(h => h.Description.ToLower() == param.ToLower());
                            if (host != null)
                            {
                                manager.RecycleHostFile(host.Description, false);
                            }
                            else
                            {
                                ShowMessage("The hosts file doesn't exist.\n");
                            }
                            break;
                        default:
                            ShowError();
                            break;
                    }
                    break;
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
            ShowSettings(false);
        }

        private static void ShowSettings(bool isDetail)
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
                        ShowMessage(File.ReadAllText(Path.Combine(HostFileManager.GetInstance().CurrentDirectory, envHost.Path)) + "\n");
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
            ShowMessage("        info\tShow current hosts file name. With [<param> all], will show.");
            ShowMessage("            \tthe detailed settings.");
            ShowMessage("        list\tList all hosts files");
            ShowMessage("        new\tCreate a new hosts file with a name [<param> hosts file name].");
            ShowMessage("        remove\tMark a hosts file as removed, cannot be used with [change]");
            ShowMessage("              \toperation.");
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
