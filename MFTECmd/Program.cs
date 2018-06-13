﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using CsvHelper;
using Fclp;
using Fclp.Internals.Extensions;
using MFT;
using MFT.Attributes;
using MFT.Other;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace MFTECmd
{
    internal class Program
    {
        private static Logger _logger;

        private static readonly string _preciseTimeFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;
        private static Mft _mft;

        private static void Main(string[] args)
        {
            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();

            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.File)
                .As('f')
                .WithDescription("File to process. Either this or -d is required");

//            _fluentCommandLineParser.Setup(arg => arg.Directory)
//                .As('d')
//                .WithDescription("Directory to recursively process. Either this or -f is required");

            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription(
                    "Directory to save CSV ormatted results to. Be sure to include the full path in double quotes. Required");

// 
//            _fluentCommandLineParser.Setup(arg => arg.JsonDirectory)
//                .As("json")
//                .WithDescription(
//                    "Directory to save json representation to. Use --pretty for a more human readable layout");

//            _fluentCommandLineParser.Setup(arg => arg.JsonPretty)
//                .As("pretty")
//                .WithDescription(
//                    "When exporting to json, use a more human readable layout\r\n").SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.Quiet)
                .As('q')
                .WithDescription(
                    "Only show the filename being processed vs all output. Useful to speed up exporting to json and/or csv\r\n")
                .SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying time stamps. Default is: yyyy-MM-dd HH:mm:ss")
                .SetDefault("yyyy-MM-dd HH:mm:ss");

            _fluentCommandLineParser.Setup(arg => arg.PreciseTimestamps)
                .As("mp")
                .WithDescription(
                    "Display higher precision for time stamps. Default is false").SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.IncludeShortNames)
                .As("sn")
                .WithDescription(
                    "Include DOS file name types. Default is false").SetDefault(false);



            var header =
                $"MFTECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/MFTECmd";


            var footer = @"Examples: MFTECmd.exe -f ""C:\Temp\SomeMFT""" + "\r\n\t " +
                         @" MFTECmd.exe -f ""C:\Temp\SomeMFT"" --csv ""c:\temp\out"" -q" + "\r\n\t " +
                         "\r\n\t" +
                         "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _logger.Error("");
                _logger.Error(result.ErrorText);

                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("-f is required. Exiting");
                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() == false &&
                !File.Exists(_fluentCommandLineParser.Object.File))
            {
                _logger.Warn($"File '{_fluentCommandLineParser.Object.File}' not found. Exiting");
                return;
            }

            if (_fluentCommandLineParser.Object.CsvDirectory.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("--csv is required. Exiting");
                return;
            }

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            if (_fluentCommandLineParser.Object.PreciseTimestamps)
            {
                _fluentCommandLineParser.Object.DateTimeFormat = _preciseTimeFormat;
            }

            if (IsAdministrator() == false)
            {
                _logger.Fatal($"Warning: Administrator privileges not found!\r\n");
            }

            var sw = new Stopwatch();
            sw.Start();

            _mft = MftFile.Load(_fluentCommandLineParser.Object.File);

            //do work here

            sw.Stop();


            if (_fluentCommandLineParser.Object.Quiet)
            {
                _logger.Info("");
            }

            _logger.Info(
                $"\r\nProcessed '{_fluentCommandLineParser.Object.File}' in {sw.Elapsed.TotalSeconds:N4} seconds");

            if (Directory.Exists(_fluentCommandLineParser.Object.CsvDirectory) == false)
            {
                _logger.Warn($"Path to '{_fluentCommandLineParser.Object.CsvDirectory}' doesn't exist. Creating...");
                Directory.CreateDirectory(_fluentCommandLineParser.Object.CsvDirectory);
            }

            var outName = $"{DateTimeOffset.Now.ToString("yyyyMMddHHmmss")}_MFTECmd_Output.csv";
            var outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);


            _logger.Warn($"\r\nCSV output will be saved to '{outFile}'");

            try
            {
                using (var sw1 = new StreamWriter(outFile, false, Encoding.UTF8))
                {
                    var csv = new CsvWriter(sw1);

                    var foo = csv.Configuration.AutoMap<MFTRecordOut>();

                    foo.Map(t => t.EntryNumber).Index(0);
                    foo.Map(t => t.SequenceNumber).Index(1);
                    foo.Map(t => t.InUse).Index(2);
                    foo.Map(t => t.ParentEntryNumber).Index(3);
                    foo.Map(t => t.ParentSequenceNumber).Index(4);
                    foo.Map(t => t.ParentPath).Index(5);
                    foo.Map(t => t.FileName).Index(6);
                    foo.Map(t => t.Extension).Index(7);
                    foo.Map(t => t.FileSize).Index(8);
                    foo.Map(t => t.ReferenceCount).Index(9);
                    foo.Map(t => t.ReparseTarget).Index(10);

                    foo.Map(t => t.IsDirectory).Index(11);
                    foo.Map(t => t.HasAds).Index(12);
                    foo.Map(t => t.IsAds).Index(13);
                    foo.Map(t => t.Timestomped).Index(14);
                    foo.Map(t => t.uSecZeros).Index(15);
                    foo.Map(t => t.SiFlags).Index(16);
                    foo.Map(t => t.NameType).Index(17);
                    
                    foo.Map(t => t.Created0x10).ConvertUsing(t =>
                        $"=\"{t.Created0x10?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(18);
                    foo.Map(t => t.Created0x30).ConvertUsing(t =>
                        $"=\"{t.Created0x30?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(19);

                    foo.Map(t => t.LastModified0x10).ConvertUsing(t =>
                        $"=\"{t.LastModified0x10?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(20);
                    foo.Map(t => t.LastModified0x30).ConvertUsing(t =>
                        $"=\"{t.LastModified0x30?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(21);

                    foo.Map(t => t.LastRecordChange0x10).ConvertUsing(t =>
                        $"=\"{t.LastRecordChange0x10?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(22);
                    foo.Map(t => t.LastRecordChange0x30).ConvertUsing(t =>
                        $"=\"{t.LastRecordChange0x30?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(23);
                   
                    foo.Map(t => t.LastAccess0x10).ConvertUsing(t =>
                        $"=\"{t.LastAccess0x10?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(24);

                    foo.Map(t => t.LastAccess0x30).ConvertUsing(t =>
                        $"=\"{t.LastAccess0x30?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}\"").Index(25);

                    foo.Map(t => t.UpdateSequenceNumber).Index(26);
                    foo.Map(t => t.LogfileSequenceNumber).Index(27);
                    foo.Map(t => t.SecurityId).Index(28);
                    
                    foo.Map(t => t.ObjectIdFileDroid).Index(29);
                    foo.Map(t => t.LoggedUtilStream).Index(30);
                    foo.Map(t => t.ZoneIdContents).Index(31);


                    csv.Configuration.RegisterClassMap(foo);

                    csv.WriteHeader<MFTRecordOut>();
                    csv.NextRecord();


                    foreach (var fr in _mft.FileRecords)
                    {
                        foreach (var attribute in fr.Value.Attributes.Where(t =>
                            t.AttributeType == AttributeType.FileName))
                        {
                            var fn = (FileName) attribute;
                            if (_fluentCommandLineParser.Object.IncludeShortNames == false && fn.FileInfo.NameType == NameTypes.Dos)
                            {
                                continue;
                            }

                            var mftr = GetCsvData(fr.Value, fn,null);

                            var ads = fr.Value.GetAlternateDataStreams();

                            mftr.HasAds = ads.Any();

                            csv.WriteRecord(mftr);
                            csv.NextRecord();
                            
                            foreach (var adsInfo in ads)
                            {
                                var adsRecord = GetCsvData(fr.Value, fn,adsInfo);
                                adsRecord.IsAds = true;
                                csv.WriteRecord(adsRecord);
                                csv.NextRecord();
                            }

                        }
                    }

                    foreach (var fr in _mft.FreeFileRecords)
                    {
                        foreach (var attribute in fr.Value.Attributes.Where(t =>
                            t.AttributeType == AttributeType.FileName))
                        {
                            var fn = (FileName) attribute;
                            if (_fluentCommandLineParser.Object.IncludeShortNames == false && fn.FileInfo.NameType == NameTypes.Dos)
                            {
                                continue;
                            }

                            var mftrD = GetCsvData(fr.Value, fn,null);

                            var ads = fr.Value.GetAlternateDataStreams();

                            mftrD.HasAds = ads.Any();

                            csv.WriteRecord(mftrD);
                            csv.NextRecord();

                            foreach (var adsInfo in ads)
                            {
                                var adsRecord = GetCsvData(fr.Value, fn,adsInfo);
                                adsRecord.IsAds = true;
                                csv.WriteRecord(adsRecord);
                                csv.NextRecord();
                            }

                        }
                    }

                    sw1.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Error exporting data. Error: {ex.Message}");
            }
        }

        public static MFTRecordOut GetCsvData(FileRecord fr, FileName fn,AdsInfo adsinfo)
        {
            var mftr = new MFTRecordOut
            {
                EntryNumber = fr.EntryNumber,
                FileName = fn.FileInfo.FileName,
                InUse = true,
                ParentPath = _mft.GetFullParentPath(fn.FileInfo.ParentMftRecord.GetKey()),
                SequenceNumber = fr.SequenceNumber,
                IsDirectory = fr.IsDirectory(),
                ParentEntryNumber =  fn.FileInfo.ParentMftRecord.MftEntryNumber,
                ParentSequenceNumber =  fn.FileInfo.ParentMftRecord.MftSequenceNumber,
                NameType = fn.FileInfo.NameType
            };

            if (mftr.IsDirectory == false)
            {
                mftr.Extension = Path.GetExtension(mftr.FileName);
            }

            if (adsinfo != null)
            {
                mftr.FileName = $"{mftr.FileName}:{adsinfo.Name}";
                mftr.FileSize = adsinfo.Size;
                mftr.Extension = Path.GetExtension(adsinfo.Name);

                if (adsinfo.Name == "Zone.Identifier")
                {
                    if (adsinfo.ResidentData != null)
                    {
                        mftr.ZoneIdContents = Encoding.GetEncoding(1252).GetString(adsinfo.ResidentData.Data);
                    }
                    else
                    {
                        mftr.ZoneIdContents = "(Zone.Identifier data is non-resident)";
                    }
                }
            }

            mftr.ReferenceCount = fr.GetReferenceCount();

            mftr.FileSize = fr.GetFileSize();
            mftr.LogfileSequenceNumber = fr.LogSequenceNumber;

            var oid = (ObjectId) fr.Attributes.SingleOrDefault(t =>
                t.AttributeType == AttributeType.VolumeVersionObjectId);

            if (oid != null)
            {
                mftr.ObjectIdFileDroid = oid.FileDroid.ToString();
            }

            var lus = (LoggedUtilityStream) fr.Attributes.FirstOrDefault(t =>
                t.AttributeType == AttributeType.LoggedUtilityStream);

            if (lus != null)
            {
                mftr.LoggedUtilStream= lus.Name;
            }

            var rp = fr.GetReparsePoint();
            if (rp != null)
            {
                mftr.ReparseTarget = rp.PrintName;
            }


            var si = (StandardInfo) fr.Attributes.SingleOrDefault(t =>
                t.AttributeType == AttributeType.StandardInformation);

            if (si != null)
            {
                mftr.UpdateSequenceNumber = si.UpdateSequenceNumber;

                mftr.Created0x10 = si.CreatedOn;
                mftr.LastModified0x10 = si.ContentModifiedOn;
                mftr.LastRecordChange0x10 = si.RecordModifiedOn;
                mftr.LastAccess0x10 = si.LastAccessedOn;

                if (fn.FileInfo.CreatedOn != si.CreatedOn)
                {
                    mftr.Created0x30 = fn.FileInfo.CreatedOn;
                }

                if (fn.FileInfo.ContentModifiedOn != si.ContentModifiedOn)
                {
                    mftr.LastModified0x30 = fn.FileInfo.CreatedOn;
                }

                if (fn.FileInfo.RecordModifiedOn != si.RecordModifiedOn)
                {
                    mftr.LastRecordChange0x30 = fn.FileInfo.CreatedOn;
                }

                if (fn.FileInfo.LastAccessedOn != si.LastAccessedOn)
                {
                    mftr.LastAccess0x30 = fn.FileInfo.CreatedOn;
                }

                mftr.SecurityId = si.SecurityId;

                mftr.SiFlags = si.Flags;

                if ((mftr.Created0x30.HasValue && mftr.Created0x10?.UtcTicks < mftr.Created0x30.Value.UtcTicks) || (mftr.LastModified0x30.HasValue && mftr.LastModified0x10?.UtcTicks < mftr.LastModified0x30.Value.UtcTicks))
                {
                    mftr.Timestomped = true;
                }

                if (mftr.Created0x10?.Millisecond == 0 || mftr.LastModified0x10?.Millisecond == 0||
                 
                    mftr.LastAccess0x10?.Millisecond == 0)
                {
                    mftr.uSecZeros = true;
                }
            }
            else
            {
                //no si, so update FN timestamps
                mftr.Created0x30 = fn.FileInfo.CreatedOn;
                mftr.LastModified0x10 = fn.FileInfo.ContentModifiedOn;
                mftr.LastRecordChange0x10 = fn.FileInfo.RecordModifiedOn;
                mftr.LastAccess0x10 = fn.FileInfo.LastAccessedOn;
            }


            return mftr;
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void SetupNLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }
    }

    internal class ApplicationArguments
    {
        public string File { get; set; }
        public string Directory { get; set; }

        // public string JsonDirectory { get; set; }
        public bool JsonPretty { get; set; }
        public string CsvDirectory { get; set; }

        public string DateTimeFormat { get; set; }

        public bool PreciseTimestamps { get; set; }

        public bool Quiet { get; set; }
        public bool IncludeShortNames { get; set; }
    }
}