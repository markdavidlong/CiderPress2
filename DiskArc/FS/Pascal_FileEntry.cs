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
using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;

using CommonUtil;
using static DiskArc.Defs;

namespace DiskArc.FS {
    public class Pascal_FileEntry : IFileEntryExt, IDisposable {
        //
        // IFileEntry interfaces.
        //

        public bool IsValid => throw new NotImplementedException();

        public bool IsDubious => throw new NotImplementedException();

        public bool IsDamaged => throw new NotImplementedException();

        public bool IsDirectory => throw new NotImplementedException();

        public bool HasDataFork => throw new NotImplementedException();
        public bool HasRsrcFork => throw new NotImplementedException();
        public bool IsDiskImage => throw new NotImplementedException();

        public IFileEntry ContainingDir => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public string FileName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public char DirectorySeparatorChar { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FullPathName => throw new NotImplementedException();
        public byte[] RawFileName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool HasProDOSTypes => throw new NotImplementedException();

        public byte FileType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ushort AuxType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool HasHFSTypes { get { return false; } }
        public uint HFSFileType { get { return 0; } set { } }
        public uint HFSCreator { get { return 0; } set { } }

        public byte Access { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime CreateWhen { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime ModWhen { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public long StorageSize => throw new NotImplementedException();

        public long DataLength => throw new NotImplementedException();

        public long RsrcLength => throw new NotImplementedException();

        public string Comment { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool GetPartInfo(FilePart part, out long length, out long storageSize, out CompressionFormat format) {
            throw new NotImplementedException();
        }

        public IEnumerator<IFileEntry> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        private void CheckChangeAllowed() {
            throw new NotImplementedException();
        }

        //
        // Implementation-specific.
        //

        public Pascal_FileEntry(Pascal fileSystem) {
            throw new NotImplementedException();
        }

        // IDisposable generic finalizer.
        ~Pascal_FileEntry() {
            Dispose(false);
        }
        // IDisposable generic Dispose() implementation.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing) {
            // The only reason for doing this is to ensure that we're flushing changes.  This
            // can happen if the application chooses not to call SaveChanges before closing
            // the filesystem or switching to raw mode.

            // TODO
        }

        /// <summary>
        /// Invalidates the file entry object.  This is called when the filesystem is switched
        /// to "raw" access mode, so that any objects retained by the application stop working.
        /// </summary>
        internal void Invalidate() {
#pragma warning disable CS8625
            throw new NotImplementedException();
#pragma warning restore CS8625
        }

        // IFileEntry
        public void SaveChanges() {
            throw new NotImplementedException();
        }

        // IFileEntry
        public void AddConflict(uint chunk, IFileEntry entry) {
            throw new NotImplementedException();
        }

        #region Filenames

        // IFileEntry
        public int CompareFileName(string fileName) {
            throw new NotImplementedException();
        }

        // IFileEntry
        public int CompareFileName(string fileName, char fileNameSeparator) {
            throw new NotImplementedException();
        }

        public static string AdjustFileName(string fileName) {
            throw new NotImplementedException();
        }

        #endregion Filenames

        public override string ToString() {
            return "TODO";
        }
    }
}
