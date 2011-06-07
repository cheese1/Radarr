﻿using System;
using System.IO;

namespace NzbDrone.Core.Providers.Core
{
    public class DiskProvider
    {
        public virtual bool FolderExists(string path)
        {
            return Directory.Exists(path);
        }

        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual string[] GetDirectories(string path)
        {
            return Directory.GetDirectories(path);
        }

        public virtual string[] GetFiles(string path, string pattern, SearchOption searchOption)
        {
            return Directory.GetFiles(path, pattern, searchOption);
        }

        public virtual long GetSize(string path)
        {
            var fi = new FileInfo(path);
            return fi.Length;
            //return new FileInfo(path).Length;
        }

        public virtual String CreateDirectory(string path)
        {
            return Directory.CreateDirectory(path).FullName;
        }

        public virtual void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public virtual void RenameFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        public virtual string GetExtension(string path)
        {
            return Path.GetExtension(path);
        }
    }
}