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
using DiskArc.Arc;
using static DiskArc.Defs;
using static DiskArc.IFileSystem;

namespace AppCommon {
    /// <summary>
    /// <para>Adds a collection of files to an IArchive or IFileSystem object, where the files are
    /// provided as a pairing of metadata and open-on-demand read-only streams.  Uses callbacks
    /// to display progress and warning messages, and to query for handling of conflicts.</para>
    /// <para>This fills the same role as <see cref="AddFileWorker"/>, but for a platform-specific
    /// clipboard paste function.</para>
    /// </summary>
    public class ClipPasteWorker {
        /// <summary>
        /// Callback function interface definition.
        /// </summary>
        public delegate CallbackFacts.Results CallbackFunc(CallbackFacts what);

        /// <summary>
        /// Stream generator function interface definition.
        /// </summary>
        /// <param name="clipEntry">Entry to generate a stream for.</param>
        /// <returns>Write-only, non-seekable output stream, or null if no stream is
        ///   available for the specified entry.</returns>
        public delegate Stream? ClipStreamGenerator(ClipFileEntry clipEntry);

        /// <summary>
        /// If set, files added to archives with compression features will be compressed using
        /// the default compression format.
        /// </summary>
        public bool DoCompress { get; set; } = true;

        /// <summary>
        /// If set, files added to a ZIP archive that have resource forks or HFS types will
        /// be stored as AppleDouble with a "__MACOSX" prefix.
        /// </summary>
        public bool EnableMacOSZip { get; set; } = false;

        /// <summary>
        /// If set, strip pathnames off of files before adding them.  For a filesystem, all
        /// files will be added to the target directory.
        /// </summary>
        public bool StripPaths { get; set; } = false;

        /// <summary>
        /// If set, use raw mode when adding files to filesystems (notably DOS 3.x).
        /// </summary>
        public bool RawMode { get; set; } = false;

        /// <summary>
        /// Callback function, for progress updates, warnings, and problem resolution.
        /// </summary>
        private CallbackFunc mFunc;

        private List<ClipFileEntry> mClipEntries;

        private ClipStreamGenerator mClipStreamGen;

        /// <summary>
        /// Application hook reference.
        /// </summary>
        private AppHook mAppHook;


        public ClipPasteWorker(List<ClipFileEntry> clipEntries, ClipStreamGenerator clipStreamGen,
                CallbackFunc func, bool doCompress, bool macZip, bool stripPaths, bool rawMode,
                AppHook appHook) {
            mClipEntries = clipEntries;
            mClipStreamGen = clipStreamGen;
            mFunc = func;
            DoCompress = doCompress;
            EnableMacOSZip = macZip;
            StripPaths = stripPaths;
            RawMode = rawMode;
            mAppHook = appHook;
        }

        public void AddFilesToArchive(IArchive archive, out bool isCancelled) {
            isCancelled = false;
            throw new NotImplementedException("soon!");
        }

        /// <summary>
        /// Adds files to a filesystem.
        /// </summary>
        /// <param name="fileSystem">Filesystem to add files to.</param>
        /// <param name="targetDir">Base directory where files are added, or NO_ENTRY if none
        ///   was specified.</param>
        /// <param name="isCancelled">Result: true if operation was cancelled.</param>
        /// <exception cref="IOException">File I/O error occurred.</exception>
        public void AddFilesToDisk(IFileSystem fileSystem, IFileEntry targetDir,
                out bool isCancelled) {
            if (targetDir != IFileEntry.NO_ENTRY) {
                Debug.Assert(targetDir.IsDirectory);
                Debug.Assert(targetDir.GetFileSystem() == fileSystem);
            }
            if (fileSystem.IsReadOnly) {
                // Should have been caught be caller.
                throw new Exception("target filesystem is read-only" +
                    (fileSystem.IsDubious ? " (damage)" : ""));
            }

            bool canRsrcFork = fileSystem.Characteristics.HasResourceForks;
            bool doStripPaths = StripPaths || !fileSystem.Characteristics.IsHierarchical;
            bool useRawMode = RawMode;

            IFileEntry targetDirEnt = (targetDir == IFileEntry.NO_ENTRY) ?
                fileSystem.GetVolDirEntry() : targetDir;

            for (int idx = 0; idx < mClipEntries.Count; idx++) {
                // Find the parts for this entry.  If the entry has both data and resource forks,
                // the data fork will come first, and the resource fork will be in the following
                // entry and have an identical filename.  (We could make this absolutely
                // unequivocal by adding a file serial number on the source side.)
                ClipFileEntry clipEntry = mClipEntries[idx];
                ClipFileEntry? dataPart = null;
                ClipFileEntry? rsrcPart = null;
                if (clipEntry.Part == FilePart.DataFork || clipEntry.Part == FilePart.RawData ||
                        clipEntry.Part == FilePart.DiskImage) {
                    dataPart = clipEntry;
                }
                int dataIdx = idx;      // used for progress counter
                if (idx < mClipEntries.Count - 1) {
                    ClipFileEntry checkEntry = mClipEntries[idx + 1];
                    if (checkEntry.Part == FilePart.RsrcFork &&
                            checkEntry.Attribs.FullPathName == clipEntry.Attribs.FullPathName) {
                        rsrcPart = checkEntry;
                        idx++;
                    }
                }

                if (doStripPaths && clipEntry.Attribs.IsDirectory) {
                    Debug.Assert(rsrcPart == null);
                    continue;
                }

                if (clipEntry.Part == FilePart.RsrcFork && !canRsrcFork) {
                    // Nothing but a resource fork, and we can't store those.  Complain and move on.
                    Debug.Assert(rsrcPart == null);
                    CallbackFacts facts = new CallbackFacts(
                        CallbackFacts.Reasons.ResourceForkIgnored,
                        clipEntry.Attribs.FullPathName, Path.DirectorySeparatorChar);
                    facts.Part = FilePart.RsrcFork;
                    mFunc(facts);
                    continue;
                }

                // Find the destination directory for this file, creating directories as
                // needed.
                string storageDir;
                if (doStripPaths) {
                    storageDir = string.Empty;
                } else {
                    storageDir = PathName.GetDirectoryName(clipEntry.Attribs.FullPathName,
                        clipEntry.Attribs.FullPathSep);
                }
                string storageName = PathName.GetFileName(clipEntry.Attribs.FullPathName,
                        clipEntry.Attribs.FullPathSep);
                IFileEntry subDirEnt;
                subDirEnt = AddFileWorker.CreateSubdirectories(fileSystem, targetDirEnt, storageDir,
                    clipEntry.Attribs.FullPathSep);

                // Add the new file to subDirEnt.  See if it already exists.
                string adjName = fileSystem.AdjustFileName(storageName);
                if (fileSystem.TryFindFileEntry(subDirEnt, adjName, out IFileEntry newEntry)) {
                    if (clipEntry.Attribs.IsDirectory && !newEntry.IsDirectory) {
                        throw new Exception("Cannot replace non-directory '" + newEntry.FileName +
                            "' with directory");
                    } else if (!clipEntry.Attribs.IsDirectory && newEntry.IsDirectory) {
                        throw new Exception("Cannot replace directory '" + newEntry.FileName +
                            "' with non-directory");
                    } else if (!clipEntry.Attribs.IsDirectory && !newEntry.IsDirectory) {
                        // File exists.  Skip or overwrite.
                        bool doSkip = false;
                        CallbackFacts facts =
                            new CallbackFacts(CallbackFacts.Reasons.FileNameExists,
                                newEntry.FullPathName, newEntry.DirectorySeparatorChar);
                        CallbackFacts.Results result = mFunc(facts);
                        switch (result) {
                            case CallbackFacts.Results.Cancel:
                                isCancelled = true;
                                return;
                            case CallbackFacts.Results.Skip:
                                doSkip = true;
                                break;
                            case CallbackFacts.Results.Overwrite:
                                break;
                            default:
                                Debug.Assert(false);
                                break;
                        }
                        if (doSkip) {
                            continue;
                        }

                        if (newEntry.IsDubious || newEntry.IsDamaged) {
                            throw new Exception("cannot overwrite damaged file: " +
                                newEntry.FullPathName);
                        }

                        // We can either delete the existing entry and create a new one, or merge
                        // with the current contents, which might be helpful if the file on disk is
                        // extended and we're only adding one fork.  The merge would retain the
                        // current file attributes, so that adding a data fork wouldn't change the
                        // file type.  (I think somebody asked about this once for CiderPress, but
                        // I never did anything about it.  I can't find the request.)
                        //
                        // For now, just delete and recreate the entry.
                        fileSystem.DeleteFile(newEntry);
                        newEntry = IFileEntry.NO_ENTRY;
                    } else {
                        // Adding a directory that already exists.
                        Debug.Assert(clipEntry.Attribs.IsDirectory && newEntry.IsDirectory);
                    }
                }

                if (newEntry == IFileEntry.NO_ENTRY) {
                    CreateMode mode = CreateMode.File;
                    if (rsrcPart != null && canRsrcFork) {
                        mode = CreateMode.Extended;
                    } else if (clipEntry.Attribs.IsDirectory) {
                        mode = CreateMode.Directory;
                    }
                    // Create file and set file type, so DOS "cooked" mode works.
                    newEntry = fileSystem.CreateFile(subDirEnt, adjName, mode,
                        clipEntry.Attribs.FileType);
                }

                if (clipEntry.Attribs.IsDirectory) {
                    // Directory already exists or we just created it.  Nothing else to do.
                    continue;
                }

                // Add data fork.
                if (dataPart != null) {
                    int progressPerc = (100 * dataIdx) / mClipEntries.Count;
                    try {
                        // Make sure we use the FilePart from the ClipFileEntry, as that selects
                        // "raw data" mode when needed.
                        using (DiskFileStream outStream = fileSystem.OpenFile(newEntry,
                                FileAccessMode.ReadWrite, dataPart.Part)) {
                            CopyFilePart(dataPart, progressPerc, newEntry.FullPathName,
                                newEntry.DirectorySeparatorChar, outStream);
                        }
                    } catch {
                        // Copy or conversion failed, clean up.
                        fileSystem.DeleteFile(newEntry);
                        throw;
                    }
                }

                if (rsrcPart != null && canRsrcFork) {
                    int progressPerc = (100 * idx) / mClipEntries.Count;
                    try {
                        Debug.Assert(rsrcPart.Part == FilePart.RsrcFork);
                        using (DiskFileStream outStream = fileSystem.OpenFile(newEntry,
                                FileAccessMode.ReadWrite, rsrcPart.Part)) {
                            CopyFilePart(rsrcPart, progressPerc, newEntry.FullPathName,
                                newEntry.DirectorySeparatorChar, outStream);
                        }
                    } catch {
                        // Copy failed, clean up.
                        fileSystem.DeleteFile(newEntry);
                        throw;
                    }
                }

                // Set types, dates, and access flags.
                clipEntry.Attribs.FileNameOnly = adjName;
                clipEntry.Attribs.CopyAttrsTo(newEntry, true);
                newEntry.SaveChanges();
            }

            isCancelled = false;
        }

        /// <summary>
        /// Copies a part (i.e fork) of a file to a disk image file stream.
        /// </summary>
        /// <param name="clipEntry">Clip file entry for this file part.</param>
        /// <param name="progressPercent">Percent complete for progress update.</param>
        /// <param name="storageName">Name of file as it appears in the filesystem.</param>
        /// <param name="storageDirSep">Directory separator char for storageName.</param>
        /// <param name="outStream">Destination stream.</param>
        /// <exception cref="IOException">Error opening source stream.</exception>
        private void CopyFilePart(ClipFileEntry clipEntry, int progressPercent,
                string storageName, char storageDirSep, DiskFileStream outStream) {
            CallbackFacts facts = new CallbackFacts(CallbackFacts.Reasons.Progress);
            facts.OrigPathName = clipEntry.Attribs.FullPathName;
            facts.OrigDirSep = clipEntry.Attribs.FullPathSep;
            facts.NewPathName = storageName;
            facts.NewDirSep = storageDirSep;
            facts.ProgressPercent = progressPercent;
            facts.Part = clipEntry.Part;
            mFunc(facts);

            using (Stream? inStream = mClipStreamGen(clipEntry)) {
                if (inStream == null) {
                    throw new IOException("Unable to open source stream");
                }
                // Simply copy the data.
                inStream.CopyTo(outStream);
            }
        }
    }
}
