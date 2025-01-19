using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WvsBeta.PatchCreator
{
    static class Program
    {
        struct InstalledVersion
        {
            public short version;
            public short subVersion;

            public string folderName;

            public int FullVersionNumber => subVersion == 0 ? version : ((version * 1000) + subVersion);

            public override string ToString()
            {
                return $"{FullVersionNumber}";
            }
            
            public static InstalledVersion FromFolderName(string name)
            {
                name = Path.GetFileName(name);
                if (!name.StartsWith("v")) throw new Exception("Invalid folder name.");
                var y = name.Substring(1).Split('.');
                var iv = new InstalledVersion
                {
                    version = short.Parse(y[0]),
                    subVersion = 0,
                    folderName = name,
                };

                if (y.Length > 1)
                {
                    iv.subVersion = short.Parse(y[1]);
                }
                else if (iv.version > 1000)
                {
                    iv.subVersion = (short)(iv.version % 1000);
                    iv.version /= 1000;
                }

                return iv;
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                var curdir = Path.Combine(Environment.CurrentDirectory, "current");
                var newdir = Path.Combine(Environment.CurrentDirectory, "new");
                CreatePatches(curdir, newdir, "Patchfile.patch");
                goto finished;
            }
            if (args[0] == "help")
            {
                Console.WriteLine("Commands:");
                Console.WriteLine("make-patcher");
                Console.WriteLine("\tMake a patcher from some input (run without extra arguments for more info)");
                Console.WriteLine("make-patches");
                Console.WriteLine("\tMake patches for multiple versions and such");
                Console.WriteLine("(no arguments)");
                Console.WriteLine("\tDefault behaviour: use the current/new folders to create Patchfile.patch");
                goto finished;
            }

            if (args[0] == "make-patcher")
            {
                if (args.Length != 6)
                {
                    Console.WriteLine("Run with arguments: from-dir to-dir base-patchfile notice-file output-file");
                    goto finished;
                }

                if (!Directory.Exists(args[1]))
                {
                    Console.WriteLine("Couldn't find from-dir.");
                    goto finished;
                }

                if (!Directory.Exists(args[2]))
                {
                    Console.WriteLine("Couldn't find to-dir.");
                    goto finished;
                }

                if (!File.Exists(args[3]))
                {
                    Console.WriteLine("Couldn't find base-patchfile.");
                    goto finished;
                }

                if (!File.Exists(args[4]))
                {
                    Console.WriteLine("Couldn't find notice-file.");
                    goto finished;
                }


                var fromDir = args[1];
                var toDir = args[2];
                var basePatchfile = args[3];
                var noticeFile = args[4];
                var outputFile = args[5];


                var patchfile = Path.Combine(Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(outputFile) + ".patch");

                CreatePatches(fromDir, toDir, patchfile);

                CreatePatcher(patchfile, outputFile, File.ReadAllBytes(basePatchfile), File.ReadAllBytes(noticeFile));
            }
            else if (args[0] == "make-patches")
            {
                var patchesDir = Path.Combine(Environment.CurrentDirectory, "patches");
                
                var availableMapleInstallations = Directory.GetDirectories(Environment.CurrentDirectory, "v*")
                    .Select(Path.GetFileName)
                    .Where(x => x.StartsWith("v"))
                    .Select(InstalledVersion.FromFolderName)
                    .ToList();


                var basePatchfileBinary = File.ReadAllBytes(Path.Combine(patchesDir, "patch.base"));


                // Build Version.info files
                var newPatcherChecksum = CRC32.CalculateChecksumFile(Path.Combine(patchesDir, "NewPatcher.dat"));


                foreach (var currentMapleInstall in availableMapleInstallations)
                {
                    var subPatch = currentMapleInstall.subVersion != 0;
                    Console.WriteLine("Maple installation found: {0}", currentMapleInstall);

                    var mapleDir = Path.Combine(patchesDir, "" + currentMapleInstall);
                    if (!Directory.Exists(mapleDir))
                        Directory.CreateDirectory(mapleDir);

                    var noticeFile = Path.Combine(mapleDir, $"{currentMapleInstall.FullVersionNumber:D5}.txt");
                    var noticeText = Encoding.ASCII.GetBytes("Please make " + noticeFile);
                    if (File.Exists(noticeFile))
                    {
                        noticeText = File.ReadAllBytes(noticeFile);
                    }
                    else
                    {
                        Console.WriteLine("Did not find the noticefile! " + noticeFile);
                    }

                    var patchables = availableMapleInstallations;
                    if (!subPatch)
                    {
                        patchables = patchables
                            .Where(x => x.version < currentMapleInstall.version)
                            .ToList();
                    }
                    else
                    {
                        patchables =
                            patchables
                                .Where(x => x.version == currentMapleInstall.version)
                                .OrderByDescending(x => x.subVersion)
                                .Where(x => x.subVersion < currentMapleInstall.subVersion)
                                .ToList();

                    }

                    patchables.AsParallel().ForAll(olderMapleInstall =>
                    {
                        var patchFilename = $"{olderMapleInstall.FullVersionNumber:D5}to{currentMapleInstall.FullVersionNumber:D5}";

                        if (!File.Exists(Path.Combine(mapleDir, patchFilename + ".patch")))
                        {
                            Console.WriteLine("Creating patchfile for {0} to {1}", olderMapleInstall, currentMapleInstall);

                            CreatePatches(
                                Path.Combine(Environment.CurrentDirectory, olderMapleInstall.folderName),
                                Path.Combine(Environment.CurrentDirectory, currentMapleInstall.folderName),
                                Path.Combine(mapleDir, patchFilename + ".patch")
                            );

                        }

                        if (!File.Exists(Path.Combine(mapleDir, patchFilename + ".exe")))
                        {
                            Console.WriteLine("Creating patcher for {0} to {1}", olderMapleInstall, currentMapleInstall);
                            CreatePatcher(
                                Path.Combine(mapleDir, patchFilename + ".patch"),
                                Path.Combine(mapleDir, patchFilename + ".exe"),
                                basePatchfileBinary,
                                noticeText
                            );
                        }
                    });


                    uint patcherChecksum = newPatcherChecksum;
                    if (File.Exists(Path.Combine(mapleDir, "NewPatcher.dat")))
                    {
                        patcherChecksum = CRC32.CalculateChecksumFile(Path.Combine(mapleDir, "NewPatcher.dat"));
                    }
                    else
                    {
                        File.Copy(
                            Path.Combine(patchesDir, "NewPatcher.dat"),
                            Path.Combine(mapleDir, "NewPatcher.dat")
                        );
                    }

                    using (var fs = File.Open(Path.Combine(mapleDir, "Version.info"), FileMode.Create))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.WriteLine($"0x{patcherChecksum:X8}");

                        foreach (var fromVersion in patchables.Select(x => x.FullVersionNumber).Distinct())
                        {
                            sw.WriteLine($"{fromVersion:D5}");
                        }
                    }
                }
            }

            else if (File.Exists(args[0]))
            {
                var crc = CRC32.CalculateChecksumFile(args[0]);
                Console.WriteLine("CRC: {0:X8}", crc);
                Console.Read();
                return;
            }


            finished:
            Console.WriteLine("Done! Press (the) any key to close");
            Console.Read();
            Console.WriteLine("Not the any key. whatever");

        }

        static void CreatePatcher(string patchFilePath, string outputFilePath, byte[] basePatchfileBinary, byte[] noticeText)
        {
            using (var patchFile = File.OpenRead(patchFilePath))
            using (var exeFile = File.OpenWrite(outputFilePath))
            using (var bw = new BinaryWriter(exeFile))
            {
                bw.Write(basePatchfileBinary);
                patchFile.Position = 0;
                patchFile.CopyTo(exeFile);
                bw.Write(noticeText);
                bw.Write((uint)patchFile.Length);
                bw.Write((uint)noticeText.Length);
                bw.Write(new byte[] { 0xF3, 0xFB, 0xF7, 0xF2 });
            }
        }

        static void CreatePatches(string curdir, string newdir, string patchfilePath)
        {

            var patches = new List<IPatchedFile>();


            if (!Directory.Exists(curdir))
            {
                Console.WriteLine("ERROR: {0} does not exist. This is the 'current' dir", curdir);
                Console.ReadLine();
                return;
            }

            if (!Directory.Exists(newdir))
            {
                Console.WriteLine("ERROR: {0} does not exist. This is the 'new' dir", newdir);
                Console.ReadLine();
                return;
            }

            var currentVersionFiles =
                Directory.EnumerateFiles(curdir, "*", SearchOption.AllDirectories)
                    .Where(x => !x.Contains(Path.DirectorySeparatorChar + "unins"))
                    .ToList();
            var newVersionFiles =
                Directory.EnumerateFiles(newdir, "*", SearchOption.AllDirectories)
                    .Where(x => !x.Contains(Path.DirectorySeparatorChar + "unins"))
                    .ToList();

            var currentVersionFilesWithoutDir = currentVersionFiles.Select(x => x.Replace(curdir, "")).ToList();
            var newVersionFilesWithoutDir = newVersionFiles.Select(x => x.Replace(newdir, "")).ToList();

            var deletedFiles = currentVersionFilesWithoutDir.Where(x => !newVersionFilesWithoutDir.Contains(x)).ToList();
            var addedFiles = newVersionFilesWithoutDir.Where(x => !currentVersionFilesWithoutDir.Contains(x)).ToList();

            foreach (var patch in patches)
            {
                if (patch is RemovedFile)
                    Console.WriteLine("Registered deletion of {0}", patch.Filename);
                else if (patch is AddedFile)
                    Console.WriteLine("Registered addition of {0}", patch.Filename);
            }

            // Remove all the added files
            var restFiles = newVersionFiles.Where(x => !addedFiles.Contains(x.Replace(newdir, ""))).ToList();

            var modifiedFiles = restFiles
                .Select(x => new FilePatchLogic(x.Replace(newdir, curdir), x))
                // Only changed files
                .Where(x => x.NewChecksum != x.OldChecksum)
                .ToList();
            
            deletedFiles = deletedFiles.Select(x => curdir + x).ToList();
            addedFiles = addedFiles.Select(x => newdir + x).ToList();


            // Do not 'update' DLLs and EXEs
            modifiedFiles = modifiedFiles.Where(x =>
            {
                if (x.Filename.EndsWith(".exe") || x.Filename.EndsWith(".dll"))
                {
                    Console.WriteLine("Adding {0} as new file", x.NewFile);
                    //deletedFiles.Add(x.Filename);
                    addedFiles.Add(x.NewFile);
                    return false;
                }

                return true;
            }).ToList();

            foreach (var filePatchLogic in modifiedFiles)
            {
                Console.WriteLine("Working on {0}", filePatchLogic.Filename);
                filePatchLogic.Init();
                filePatchLogic.Run();
                Console.WriteLine("Done, cleaning up...");
                filePatchLogic.Cleanup();
            }

            patches.AddRange(modifiedFiles);

            patches.AddRange(deletedFiles.Select(x => new RemovedFile() { Filename = x }));
            patches.AddRange(addedFiles.Select(x => new AddedFile() { Filename = x }));


            PatchFile.BuildPatchfile(
                newdir,
                curdir,
                patchfilePath,
                patches.ToArray()
            );

        }
    }
}
