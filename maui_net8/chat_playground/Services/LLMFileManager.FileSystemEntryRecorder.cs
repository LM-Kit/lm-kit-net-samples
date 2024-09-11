using ChatPlayground.Helpers;
using System.Diagnostics;

namespace ChatPlayground.Services;

public partial class LLMFileManager
{
    private sealed class FileSystemEntryRecorder
    {
        private DirectoryRecord? _rootDirectoryRecord;
        private Uri? _rootDirectoryUri;

        public void Init(string rootDirectoryPath)
        {
            if (_rootDirectoryRecord != null)
            {
                Clear();
            }

            _rootDirectoryUri = new Uri(rootDirectoryPath);
            _rootDirectoryRecord = new DirectoryRecord(rootDirectoryPath, null);
        }

        public void Clear()
        {
            _rootDirectoryRecord!.Entries.Clear();
        }

        public FileRecord? RecordFile(Uri fileUri)
        {
            string fileBaseName = FileHelpers.GetFileBaseName(fileUri);
            DirectoryRecord directParentDirectory = TryGetDirectParentDirectory(fileUri, true)!;
            FileRecord? file = directParentDirectory!.TryGetChildFile(fileBaseName);

            if (file != null)
            {
                // This file is already recorded
                return null;
            }
            else
            {
                FileRecord fileRecord = new FileRecord(fileUri, fileBaseName, directParentDirectory);

                directParentDirectory.AddEntry(fileRecord);

                return fileRecord;
            }
        }

        public FileRecord? DeleteFileRecord(Uri fileUri)
        {
            var entry = TryGetExistingEntry(fileUri);

            if (entry != null && entry.Delete() && entry is FileRecord fileRecord)
            {
                return fileRecord;
            }

            return null;
        }

        public FileSystemEntryRecord? TryGetExistingEntry(Uri fileUri)
        {
            int depth = fileUri.Segments.Length - _rootDirectoryUri!.Segments.Length;
            DirectoryRecord current = _rootDirectoryRecord!;

            DirectoryRecord? parentDirectory = TryGetDirectParentDirectory(fileUri);

            if (parentDirectory != null)
            {
                return parentDirectory.TryGetChildEntry(FileHelpers.GetFileBaseName(fileUri));
            }
            else
            {
                return null;
            }
        }

        private DirectoryRecord? TryGetDirectParentDirectory(Uri fileUri, bool createIfNonExisting = false)
        {
            int targetDepth = fileUri.Segments.Length - _rootDirectoryUri!.Segments.Length - 1;
            DirectoryRecord current = _rootDirectoryRecord!;

            for (int currentLevel = 0; currentLevel < targetDepth; currentLevel++)
            {
                int segmentIndex = fileUri.Segments.Length - (targetDepth - currentLevel) - 1;
                string entryName = FileHelpers.SanitizeUriSegment(fileUri.Segments[segmentIndex]);

                DirectoryRecord? childDirectory = current.TryGetChildDirectory(entryName);

                if (childDirectory == null)
                {
                    if (!createIfNonExisting)
                    {
                        return null;
                    }
                    else
                    {
                        childDirectory = new DirectoryRecord(entryName, current);
                        current.AddEntry(childDirectory);
                    }
                }

                current = childDirectory;
            }

            return current;
        }

        public static List<FileRecord> GetAllChildFiles(DirectoryRecord directoryRecord)
        {
            List<FileRecord> files = new List<FileRecord>();

            CollectChildFiles(directoryRecord, files);

            return files;
        }

        private static void CollectChildFiles(DirectoryRecord directoryRecord, List<FileRecord> files)
        {
            foreach (var entry in directoryRecord.Entries)
            {
                if (entry is DirectoryRecord subDirectory)
                {
                    CollectChildFiles(subDirectory, files);
                }
                else if (entry is FileRecord fileRecord)
                {
                    files.Add(fileRecord);
                }
            }
        }


#if DEBUG
        public void DumpRecords()
        {
            DumpRecords(_rootDirectoryRecord!);
        }

        private void DumpRecords(DirectoryRecord directory, int level = 0)
        {
            Trace.Write(new string(' ', level * 5));
            Trace.WriteLine(directory.Name);

            foreach (var entry in directory.Entries)
            {
                if (entry is DirectoryRecord childDirectory)
                {
                    DumpRecords(childDirectory, level + 1);
                }
                else if (entry is FileRecord file)
                {
                    Trace.Write(new string(' ', (level + 1) * 5));
                    Trace.WriteLine(file.Name);
                }
            }
        }
#endif

        public abstract class FileSystemEntryRecord
        {
            public DirectoryRecord? Parent { get; protected set; }

            public string Name { get; private set; }

            public FileSystemEntryRecord(string name, DirectoryRecord? parent)
            {
                Name = name;
                Parent = parent;
            }

            protected abstract void OnEntryRenamed();

            public void Rename(string name)
            {
                Name = name;
                OnEntryRenamed();
            }

            public bool Delete()
            {
                if (Parent == null)
                {
                    return false;
                }

                return Parent.DeleteEntry(this);
            }
        }

        public class FileRecord : FileSystemEntryRecord
        {
            public Uri FileUri { get; set; }

            public event EventHandler? FilePathChanged;

            public FileRecord(Uri fileUri, string name, DirectoryRecord parent) : base(name, parent)
            {
                FileUri = fileUri;
            }

            public void OnParentDirectoryRenamed(int ancestorLevel, string newName)
            {
                Uri oldUri = FileUri;
                FileUri = FileHelpers.GetRenamedFileUri(FileUri, newName, ancestorLevel);
                FilePathChanged?.Invoke(this, new FileRecordPathChangedEventArgs(oldUri, FileUri));
            }

            protected override void OnEntryRenamed()
            {
                Uri oldUri = FileUri;
                FileUri = FileHelpers.GetRenamedFileUri(FileUri, Name);
                FilePathChanged?.Invoke(this, new FileRecordPathChangedEventArgs(oldUri, FileUri));
            }

#if DEBUG
            public override string ToString()
            {
                return ("(file)" + Name);
            }
#endif
        }

        public class DirectoryRecord : FileSystemEntryRecord
        {
            public List<FileSystemEntryRecord> Entries { get; protected set; }

            public DirectoryRecord(string name, DirectoryRecord? parent) : base(name, parent)
            {
                Entries = new List<FileSystemEntryRecord>();
            }

            public bool DeleteEntry(FileSystemEntryRecord entry)
            {
                return Entries.Remove(entry);
            }

            public void AddEntry(FileSystemEntryRecord entry)
            {
                Entries.Add(entry);
            }

            public DirectoryRecord? TryGetChildDirectory(string name)
            {
                foreach (var child in Entries)
                {
                    if (child is DirectoryRecord childDirectory && string.CompareOrdinal(child.Name, name) == 0)
                    {
                        return childDirectory;
                    }
                }

                return null;
            }

            public FileRecord? TryGetChildFile(string name)
            {
                foreach (var child in Entries)
                {
                    if (child is FileRecord childFile && string.CompareOrdinal(childFile.Name, name) == 0)
                    {
                        return childFile;
                    }
                }

                return null;
            }

            public FileSystemEntryRecord? TryGetChildEntry(string name)
            {
                foreach (var entry in Entries)
                {
                    if (string.CompareOrdinal(entry.Name, name) == 0)
                    {
                        return entry;
                    }
                }

                return null;
            }

            protected override void OnEntryRenamed()
            {
                PropagateRenamedParentEntryToChildren(1, Name, this);
            }

            private void PropagateRenamedParentEntryToChildren(int ancestorLevel, string newName, DirectoryRecord directoryRecord)
            {
                foreach (var child in directoryRecord.Entries)
                {
                    if (child is FileRecord childFile)
                    {
                        childFile.OnParentDirectoryRenamed(ancestorLevel, newName);
                    }
                    else if (child is DirectoryRecord childDirectory)
                    {
                        PropagateRenamedParentEntryToChildren(ancestorLevel + 1, newName, childDirectory);
                    }
                }
            }

#if DEBUG
            public override string ToString()
            {
                return ("(directory)" + Name);
            }
#endif
        }

        public sealed class FileRecordPathChangedEventArgs : EventArgs
        {
            public Uri OldPath { get;  }

            public Uri NewPath { get;  }

            public FileRecordPathChangedEventArgs(Uri oldPath, Uri newPath)
            {
                OldPath = oldPath;
                NewPath = newPath;
            }
        }
    }
}