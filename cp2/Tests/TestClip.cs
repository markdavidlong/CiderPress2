﻿/*
 * Copyright 2023 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Diagnostics;
using System.Text;

using AppCommon;
using CommonUtil;
using DiskArc;
using DiskArc.Arc;
using DiskArc.Disk;
using DiskArc.FS;
using static DiskArc.Defs;
using static DiskArc.IFileSystem;

namespace cp2.Tests {
    /// <summary>
    /// Test clipboard copy/paste infrastructure.  This does not actually use the system clipboard.
    /// </summary>
    /// <remarks>
    /// This is primarily a test of the AppCommon infrastructure rather than a test of cp2
    /// application code, but it's convenient to do it here.
    /// </remarks>
    internal static class TestClip {
        public static void RunTest(ParamsBag parms) {
            Controller.Stdout.WriteLine("  Clip...");
            Controller.PrepareTestTmp(parms);

            //
            // Basic plan:
            // - Use ClipFileSet to generate a list of ClipFileEntry objects.
            // - Simple test: compare the set to a set of expected results to confirm that
            //   the set is being generated correctly.
            // - Full test: use ClipFileWorker to add the entries to a different archive or to
            //   extract to the filesystem.  A custom stream generator is used in place of the
            //   system clipboard mechanism.
            //
            // Some things to test:
            // - Direct file transfer of entries with data, rsrc, data+rsrc.
            // - Export/extract to filesystem with attribute preservation.
            // - Filesystem copy with re-rooted source/target.
            // - DOS text conversion.
            // - MacZip at source and destination (rsrc fork, attributes).
            //

            parms.Preserve = ExtractFileWorker.PreserveMode.NAPS;
            parms.StripPaths = false;
            parms.Raw = false;
            parms.MacZip = true;

            using IArchive testArchive = CreateTestArchive(sSampleFiles, parms);
            using IDiskImage testProFS = CreateTestProDisk(sSampleFiles, parms);

            TestForeignExtract(testArchive, parms);
            TestBasicXfer((IFileSystem)testProFS.Contents!, parms);
            TestRerootXfer((IFileSystem)testProFS.Contents!, parms);

            Controller.RemoveTestTmp(parms);
        }

        private const string DATA_ONLY_NAME = "DataOnly";
        private const string RSRC_ONLY_NAME = "RsrcOnly";
        private const string DATA_RSRC_NAME = "DataRsrc";
        private const string SUBDIR1 = "subdir1";
        private const string SUBDIR2 = "subdir2";
        private const int DATA_ONLY_DLEN = 1234;
        private const int RSRC_ONLY_RLEN = 4321;
        private const int DATA_RSRC_DLEN = 2345;
        private const int DATA_RSRC_RLEN = 5432;

        private static readonly DateTime CREATE_WHEN = new DateTime(1977, 6, 1, 1, 2, 0);
        private static readonly DateTime MOD_WHEN = new DateTime(1986, 9, 15, 4, 5, 0);
        private const byte PRODOS_FILETYPE = 0x06;
        private const ushort PRODOS_AUXTYPE = 0x12cd;
        private const uint HFS_FILETYPE = 0x54455354;       // 'TEST'
        private const uint HFS_CREATOR = 0x23435032;        // '#CP2'
        private const string NAPS_STR = "#0612cd";
        private const string NAPS_STR_R = NAPS_STR + "r";
        private const char DIR_SEP = ':';

        private class SampleFile {
            public string PathName { get; private set; }
            public int DataLength { get; private set; }
            public int RsrcLength { get; private set; }

            public SampleFile(string pathName, int dataLength, int rsrcLength) {
                PathName = pathName;
                DataLength = dataLength;
                RsrcLength = rsrcLength;
            }
        }

        private static SampleFile[] sSampleFiles = new SampleFile[] {
            new SampleFile(DATA_ONLY_NAME, DATA_ONLY_DLEN, -1),
            new SampleFile(RSRC_ONLY_NAME, -1, RSRC_ONLY_RLEN),
            new SampleFile(DATA_RSRC_NAME, DATA_RSRC_DLEN, DATA_RSRC_RLEN),
            new SampleFile(SUBDIR1 + DIR_SEP + DATA_ONLY_NAME, DATA_ONLY_DLEN, -1),
            new SampleFile(SUBDIR1 + DIR_SEP + SUBDIR2 + DIR_SEP + DATA_ONLY_NAME,
                DATA_ONLY_DLEN, -1),
        };

        private static IArchive CreateTestArchive(SampleFile[] specs, ParamsBag parms) {
            NuFX arc = NuFX.CreateArchive(parms.AppHook);
            arc.StartTransaction();
            foreach (SampleFile sample in specs) {
                IFileEntry newEntry = arc.CreateRecord();
                newEntry.FileName = sample.PathName;
                newEntry.FileType = PRODOS_FILETYPE;
                newEntry.AuxType = PRODOS_AUXTYPE;
                newEntry.CreateWhen = CREATE_WHEN;
                newEntry.ModWhen = MOD_WHEN;
                if (sample.DataLength >= 0) {
                    IPartSource dataSource = Controller.CreateSimpleSource(sample.DataLength, 0x11);
                    arc.AddPart(newEntry, FilePart.DataFork, dataSource, CompressionFormat.Default);
                }
                if (sample.RsrcLength >= 0) {
                    IPartSource rsrcSource = Controller.CreateSimpleSource(sample.RsrcLength, 0x22);
                    arc.AddPart(newEntry, FilePart.RsrcFork, rsrcSource, CompressionFormat.Default);
                }
            }
            arc.CommitTransaction(new MemoryStream());
            return arc;
        }

        private static IDiskImage CreateTestProDisk(SampleFile[] specs, ParamsBag parms) {
            // Create a disk image in a memory stream.
            UnadornedSector disk = UnadornedSector.CreateBlockImage(new MemoryStream(),
                1600, parms.AppHook);
            disk.FormatDisk(FileSystemType.ProDOS, "Test", 0, true, parms.AppHook);
            IFileSystem fs = (IFileSystem)disk.Contents!;
            fs.PrepareFileAccess(true);

            foreach (SampleFile sample in specs) {
                string dirName = PathName.GetDirectoryName(sample.PathName, DIR_SEP);
                string fileName = PathName.GetFileName(sample.PathName, DIR_SEP);

                IFileEntry subDir = fs.CreateSubdirectories(fs.GetVolDirEntry(), dirName, DIR_SEP);
                IFileEntry newEntry = fs.CreateFile(subDir, fs.AdjustFileName(fileName),
                    sample.RsrcLength >= 0 ? CreateMode.Extended : CreateMode.File);
                newEntry.FileName = fileName;
                newEntry.FileType = PRODOS_FILETYPE;
                newEntry.AuxType = PRODOS_AUXTYPE;
                newEntry.CreateWhen = CREATE_WHEN;
                newEntry.ModWhen = MOD_WHEN;
                if (sample.DataLength >= 0) {
                    using (Stream stream = fs.OpenFile(newEntry, FileAccessMode.ReadWrite,
                            FilePart.DataFork)) {
                        for (int i = 0; i < sample.DataLength; i++) {
                            stream.WriteByte(0x33);
                        }
                    }
                }
                if (sample.RsrcLength >= 0) {
                    using (Stream stream = fs.OpenFile(newEntry, FileAccessMode.ReadWrite,
                            FilePart.RsrcFork)) {
                        for (int i = 0; i < sample.RsrcLength; i++) {
                            stream.WriteByte(0x44);
                        }
                    }
                }
            }
            return disk;
        }

        private class ExpectedForeign {
            public string PathName { get; private set; }
            public int MinLength { get; private set; }
            public int MaxLength { get; private set; }

            public ExpectedForeign(string fileName, int length) {
                PathName = fileName;
                MinLength = MaxLength = length;
            }
            public ExpectedForeign(string fileName, int minLength, int maxLength) {
                PathName = fileName;
                MinLength = minLength;
                MaxLength = maxLength;
            }
        }

        private static ExpectedForeign[] sForeignExtractNAPS = new ExpectedForeign[] {
            new ExpectedForeign(DATA_ONLY_NAME + NAPS_STR, DATA_ONLY_DLEN),
            new ExpectedForeign(RSRC_ONLY_NAME + NAPS_STR_R, RSRC_ONLY_RLEN),
            new ExpectedForeign(DATA_RSRC_NAME + NAPS_STR, DATA_RSRC_DLEN),
            new ExpectedForeign(DATA_RSRC_NAME + NAPS_STR_R, DATA_RSRC_RLEN),
            new ExpectedForeign(SUBDIR1, -1),
            new ExpectedForeign(Path.Join(SUBDIR1, DATA_ONLY_NAME + NAPS_STR), DATA_ONLY_DLEN),
            new ExpectedForeign(Path.Join(SUBDIR1, SUBDIR2), -1),
            new ExpectedForeign(Path.Join(SUBDIR1, SUBDIR2, DATA_ONLY_NAME + NAPS_STR),
                DATA_ONLY_DLEN),
        };

        private static void TestForeignExtract(IArchive arc, ParamsBag parms) {
            List<IFileEntry> entries = new List<IFileEntry>();
            foreach (IFileEntry entry in arc) {
                entries.Add(entry);
            }
            ClipFileSet clipSet = new ClipFileSet(arc, entries, IFileEntry.NO_ENTRY,
                parms.Preserve, parms.Raw, parms.StripPaths, parms.MacZip,
                null, null, parms.AppHook);

            TestForeign(clipSet, sForeignExtractNAPS, parms);
        }

        private static void TestForeign(ClipFileSet clipSet, ExpectedForeign[] expected,
                ParamsBag parms) {
            if (clipSet.ForeignEntries.Count != expected.Length) {
                throw new Exception("Foreign extract count mismatch: expected=" + expected.Length +
                    ", actual=" + clipSet.ForeignEntries.Count);
            }
            for (int i = 0; i < clipSet.ForeignEntries.Count; i++) {
                ClipFileEntry clipEntry = clipSet.ForeignEntries[i];
                ExpectedForeign exp = expected[i];
                if (clipEntry.ExtractPath != exp.PathName) {
                    throw new Exception("Foreign mismatch " + i + ": expected='" + exp.PathName +
                        "', actual='" + clipEntry.ExtractPath + "'");
                }
                if (clipEntry.Attribs.IsDirectory) {
                    continue;
                }
                if (clipEntry.Attribs.FileType != PRODOS_FILETYPE ||
                        clipEntry.Attribs.AuxType != PRODOS_AUXTYPE ||
                        clipEntry.Attribs.CreateWhen != CREATE_WHEN ||
                        clipEntry.Attribs.ModWhen != MOD_WHEN) {
                    throw new Exception("Foreign attribute mismatch " + i + ": " +
                        clipEntry.ExtractPath);
                }
                // Test reported length.  In many cases the output length can't be known.
                if (clipEntry.OutputLength >= 0 &&
                        (clipEntry.OutputLength < exp.MinLength ||
                            clipEntry.OutputLength > exp.MaxLength)) {
                    throw new Exception("Foreign output length mismatch " + i + ": expected=" +
                        exp.MinLength + "," + exp.MaxLength + ", actual=" + clipEntry.OutputLength);
                }
            }
        }

        private class ExpectedXfer {
            public string PathName { get; private set; }
            public FilePart Part { get; private set; }
            public int Length { get; private set; }

            public ExpectedXfer(string pathName, FilePart part, int length) {
                PathName = pathName;
                Part = part;
                Length = length;
            }
        }

        private static ExpectedXfer[] sXferProDOS = new ExpectedXfer[] {
            new ExpectedXfer(DATA_ONLY_NAME, FilePart.DataFork, DATA_ONLY_DLEN),
            new ExpectedXfer(RSRC_ONLY_NAME, FilePart.DataFork, 0),
            new ExpectedXfer(RSRC_ONLY_NAME, FilePart.RsrcFork, RSRC_ONLY_RLEN),
            new ExpectedXfer(DATA_RSRC_NAME, FilePart.DataFork, DATA_RSRC_DLEN),
            new ExpectedXfer(DATA_RSRC_NAME, FilePart.RsrcFork, DATA_RSRC_RLEN),
            new ExpectedXfer(SUBDIR1, FilePart.Unknown, -1),
            new ExpectedXfer(SUBDIR1 + '/' + DATA_ONLY_NAME, FilePart.DataFork, DATA_ONLY_DLEN),
            new ExpectedXfer(SUBDIR1 + '/' + SUBDIR2, FilePart.Unknown, -1),
            new ExpectedXfer(SUBDIR1 + '/' + SUBDIR2 + '/' + DATA_ONLY_NAME, FilePart.DataFork,
                DATA_ONLY_DLEN),
        };

        private static void TestBasicXfer(IFileSystem srcFs, ParamsBag parms) {
            List<IFileEntry> entries = GatherDiskEntries(srcFs, srcFs.GetVolDirEntry());

            ClipFileSet clipSet = new ClipFileSet(srcFs, entries, IFileEntry.NO_ENTRY,
                parms.Preserve, parms.Raw, parms.StripPaths, parms.MacZip,
                null, null, parms.AppHook);

            TestXfer(clipSet, sXferProDOS, parms);
        }

        private static ExpectedXfer[] sXferProDOSReroot = new ExpectedXfer[] {
            new ExpectedXfer(DATA_ONLY_NAME, FilePart.DataFork, DATA_ONLY_DLEN),
            new ExpectedXfer(SUBDIR2, FilePart.Unknown, -1),
            new ExpectedXfer(SUBDIR2 + '/' + DATA_ONLY_NAME, FilePart.DataFork, DATA_ONLY_DLEN),
        };

        private static void TestRerootXfer(IFileSystem srcFs, ParamsBag parms) {
            IFileEntry rootDir = srcFs.GetVolDirEntry();
            IFileEntry baseDir = srcFs.FindFileEntry(rootDir, SUBDIR1);
            // Deliberately use rootDir here rather than baseDir to exercise exclusion of
            // entries that live below the root.
            List<IFileEntry> entries = GatherDiskEntries(srcFs, rootDir);

            ClipFileSet clipSet = new ClipFileSet(srcFs, entries, baseDir,
                parms.Preserve, parms.Raw, parms.StripPaths, parms.MacZip,
                null, null, parms.AppHook);

            TestXfer(clipSet, sXferProDOSReroot, parms);
        }

        private static List<IFileEntry> GatherDiskEntries(IFileSystem fs, IFileEntry rootDir) {
            List<IFileEntry> entryList = new List<IFileEntry>();
            GetDiskEntries(fs, rootDir, entryList);
            return entryList;
        }

        private static void GetDiskEntries(IFileSystem fs, IFileEntry dir,
                List<IFileEntry> entryList) {
            foreach (IFileEntry entry in dir) {
                entryList.Add(entry);
                if (entry.IsDirectory) {
                    GetDiskEntries(fs, entry, entryList);
                }
            }
        }

        private static void TestXfer(ClipFileSet clipSet, ExpectedXfer[] expected, ParamsBag parms) {
            if (clipSet.XferEntries.Count != expected.Length) {
                throw new Exception("Xfer extract count mismatch: expected=" + expected.Length +
                    ", actual=" + clipSet.XferEntries.Count);
            }
            for (int i = 0; i < clipSet.XferEntries.Count; i++) {
                ClipFileEntry clipEntry = clipSet.XferEntries[i];
                ExpectedXfer exp = expected[i];
                if (clipEntry.ExtractPath != exp.PathName) {
                    throw new Exception("Xfer mismatch " + i + ": expected='" + exp.PathName +
                        "', actual='" + clipEntry.ExtractPath + "'");
                }
                if (clipEntry.Attribs.IsDirectory) {
                    continue;
                }
                if (clipEntry.Part != exp.Part) {
                    throw new Exception("Xfer part mismatch " + i + ": expected=" + exp.Part +
                        ", actual=" + clipEntry.Part);
                }
                if (clipEntry.Attribs.FileType != PRODOS_FILETYPE ||
                        clipEntry.Attribs.AuxType != PRODOS_AUXTYPE ||
                        clipEntry.Attribs.CreateWhen != CREATE_WHEN ||
                        clipEntry.Attribs.ModWhen != MOD_WHEN) {
                    throw new Exception("Xfer attribute mismatch " + i + ": " +
                        clipEntry.ExtractPath);
                }
                // Test reported length.  In many cases the output length can't be known.
                if (clipEntry.OutputLength >= 0 && clipEntry.OutputLength != exp.Length) {
                    throw new Exception("Xfer output length mismatch " + i + ": expected=" +
                        exp.Length + ", actual=" + clipEntry.OutputLength);
                }
            }
        }
    }
}
