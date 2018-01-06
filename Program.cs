using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using CommandLine;
using CommandLine.Text; //HelpText
using Microsoft.Ajax.Utilities;
using System.Diagnostics;

namespace SEOTools
{
    // Define a class to receive parsed values
    class Options
    {
        [Option('m', "mode", DefaultValue = "FI",
          HelpText = "Mode to run. FO: reading files recursively from the folder path, FI: reading files in the file path.")]
        public string Mode { get; set; }

        [Option('i', "inputpath", Required = true,
          HelpText = "Path to the folder or file for reading content.")]
        public string InPath { get; set; }

        [Option('o', "outputpath", DefaultValue = "XXX",
          HelpText = "Path to the folder or file for writing content.")]
        public string OutPath { get; set; }

        [Option('s', "subpath", Required = false,
          HelpText = "Path to the folder for subtracting.")]
        public string SubPath { get; set; }

        [Option('q', "quality", DefaultValue = "85",
          HelpText = "Quality for compressing image.")]
        public string Quality { get; set; }

        [Option('t', "tool", DefaultValue = "C",
          HelpText = "Caesium or ImageMagick.")]
        public string Tool { get; set; }

        [Option('v', "verbose", DefaultValue = true,
          HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    class ImageCompressor
    {
        string OutPath;
        string SubPath;
        string Quality;
        string Tool;
        List<string> imageFiles = null;

        public ImageCompressor(List<string> files, string o, string s, string q, string t)
        {
            OutPath = o; SubPath = s; Quality = q; Tool = t;
            imageFiles = files;
        }

        public void Compress()
        {
            Console.WriteLine("OutPath: {0}", OutPath);
            if (!Directory.Exists(OutPath))
            {
                Console.WriteLine("Not found OutPath!");
                return;
            }
            string BackupFolder = OutPath + @"\backup";
            if (!Directory.Exists(BackupFolder)) Directory.CreateDirectory(BackupFolder);
            try
            {
                for (int i = 0; i < imageFiles.Count; i++)
                {
                    Console.WriteLine("Image: {0}", imageFiles[i]);

                    //backup it
                    string subfolder = BackupFolder + Path.GetDirectoryName(imageFiles[i]).Substring(SubPath.Length);
                    if (!Directory.Exists(subfolder)) Directory.CreateDirectory(subfolder);
                    string filename = @"\" + Path.GetFileName(imageFiles[i]);
                    if(!File.Exists(subfolder + filename)) File.Copy(imageFiles[i], subfolder + filename);

                    string d = Directory.GetCurrentDirectory();
                    Process p = new Process();
                    p.StartInfo.FileName = Tool == "C" ? d + @"\caesiumclt.exe" : d + @"\convert.exe";
                    //p.StartInfo.Arguments = " -q 0 -e -o \"" + OutPath + "\" \"" + imageFiles[i] + "\"";
                    p.StartInfo.Arguments = GetArguments(imageFiles[i], filename);
                    p.StartInfo.WorkingDirectory = d;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                    p.WaitForExit();

                    //copy overwrite to the original
                    File.Copy(OutPath + filename, imageFiles[i], true);
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private string GetArguments(string fullfilepath, string filename)
        {
            string agrs = "";
            if (filename.ToLower().EndsWith(".png"))
            {
                if (Tool == "C")
                    agrs = " -q 0 -e -o \"" + OutPath + "\" \"" + fullfilepath + "\"";
                else
                    agrs = " \"" + fullfilepath + "\" -strip " + "\"" + OutPath + filename + "\"";
            }
            else //jpeg
            {
                if (Tool == "C")
                    agrs = " -q " + Quality + " -e -o \"" + OutPath + "\" \"" + fullfilepath + "\"";
                else
                    agrs = " \"" + fullfilepath + "\" -sampling-factor 4:2:0 -strip -quality " + Quality
                           + " -interlace JPEG -colorspace sRGB " + "\"" + OutPath + filename + "\"";
            }
            return agrs;
        }
    }

    class CssJsCompressor
    {
        bool BackupOverwrite;
        string BackupSuffix;
        List<string> cssjsFiles = null;

        public CssJsCompressor(string m, string i, List<string> files)
        {
            BackupSuffix = ConfigurationManager.AppSettings["BackupSuffix"];
            BackupOverwrite = ConfigurationManager.AppSettings["BackupOverwrite"] == "1" ? true : false;
            cssjsFiles = files;
        }

        public void Compress()
        {
            Console.WriteLine("BackupSuffix: {0}", BackupSuffix);
            for(int i=0; i < cssjsFiles.Count; i++)
            {
                Console.WriteLine("File: {0}", cssjsFiles[i]);
                string source;
                using (var inputFile = new StreamReader(cssjsFiles[i]))
                {
                    source = inputFile.ReadToEnd();
                }
                var minifier = new Minifier();
                string des = "";
                if (cssjsFiles[i].EndsWith(".js"))
                {
                    des = minifier.MinifyJavaScript(source);
                    if(BackupOverwrite) File.Copy(cssjsFiles[i], cssjsFiles[i].Replace(".js", BackupSuffix + ".js"));
                    else
                    {
                        for(int k = 1; k < 1000; k++)
                        {
                            string copyFile = cssjsFiles[i].Replace(".js", BackupSuffix + k.ToString() + ".js");
                            if (k == 999 || !File.Exists(copyFile))
                            {
                                File.Copy(cssjsFiles[i], copyFile);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    des = minifier.MinifyStyleSheet(source);
                    if (BackupOverwrite) File.Copy(cssjsFiles[i], cssjsFiles[i].Replace(".css", BackupSuffix + ".css"));
                    else
                    {
                        for (int k = 1; k < 1000; k++)
                        {
                            string copyFile = cssjsFiles[i].Replace(".css", BackupSuffix + k.ToString() + ".css");
                            if (k == 999 || !File.Exists(copyFile))
                            {
                                File.Copy(cssjsFiles[i], copyFile);
                                break;
                            }
                        }
                    }
                }
                File.WriteAllText(cssjsFiles[i], des);
            }
        }
    }

    class Program
    {
        static void GetFiles(string folder, List<string> files)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(folder))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        Console.WriteLine(f);
                        files.Add(f);
                    }
                    GetFiles(d, files);
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void Main(string[] args)
        {
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if(options.OutPath == "XXX") options.OutPath = ConfigurationManager.AppSettings["DefaultOutPath"];

                List<string> iFiles = new List<string>();
                if (options.Mode.ToUpper() == "FI") iFiles = File.ReadAllLines(options.InPath).ToList();
                else GetFiles(options.InPath, iFiles);

                if (iFiles.Count < 1)
                {
                    Console.WriteLine("No files for optimizing!");
                    return;
                }

                List<string> cssjsFiles = new List<string>();
                List<string> imageFiles = new List<string>();
                string backup = ConfigurationManager.AppSettings["BackupSuffix"];

                foreach (string file in iFiles)
                {
                    if (!file.Contains(backup) && !file.Contains(".min.") && (file.ToLower().EndsWith(".css") || file.ToLower().EndsWith(".js")))
                        cssjsFiles.Add(file);
                    else if (!file.Contains(backup) && (file.ToLower().EndsWith(".png") || file.ToLower().EndsWith(".jpg")
                        || file.ToLower().EndsWith(".jpeg")))
                        imageFiles.Add(file);
                }

                if(cssjsFiles.Count > 0)
                {
                    CssJsCompressor cjc = new CssJsCompressor(options.Mode, options.InPath, cssjsFiles);
                    cjc.Compress();
                }

                if(imageFiles.Count > 0)
                {
                    string s = options.SubPath;
                    if (string.IsNullOrWhiteSpace(s)) s = options.InPath;
                    ImageCompressor ic = new ImageCompressor(imageFiles, options.OutPath,  s, options.Quality, options.Tool);
                    ic.Compress();
                }
            }

        }
    }
}
