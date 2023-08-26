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

using CommonUtil;
using DiskArc;
using static DiskArc.Defs;
using static DiskArc.IFileSystem;

namespace DiskArcTests {
    public class TestGutenberg_Prefab : ITest {
        private const string FILE1 = "gutenberg/gjr-data.do";

        private static List<Helper.FileAttr> sFile1List = new List<Helper.FileAttr>() {
            new Helper.FileAttr("DIR", 250, 256),
            new Helper.FileAttr("TEXT", 250, 256),
            new Helper.FileAttr("LONGER", 500, 512),
            new Helper.FileAttr("ANOTHER", 250, 256),
        };

        public static void TestSimple(AppHook appHook) {
            CheckDisk(FILE1, sFile1List, appHook);
        }

        private static void CheckDisk(string fileName, List<Helper.FileAttr> expFiles,
                AppHook appHook) {
            using (Stream dataFile = Helper.OpenTestFile(fileName, true, appHook)) {
                using (IDiskImage diskImage = FileAnalyzer.PrepareDiskImage(dataFile,
                        FileKind.UnadornedSector, appHook)!) {
                    diskImage.AnalyzeDisk();
                    IFileSystem fs = (IFileSystem)diskImage.Contents!;
                    fs.PrepareFileAccess(true);
                    Helper.CheckNotes(fs, 0, 0);

                    IFileEntry volDir = fs.GetVolDirEntry();
                    Helper.ValidateDirContents(volDir, expFiles);
                }
            }
        }
    }
}
