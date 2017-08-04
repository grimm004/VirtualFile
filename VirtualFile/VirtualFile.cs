using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;

namespace VirtualFile
{
    // Error Codes:
    // FileNotFoundException Thrown -1
    // FileLoadException Thrown -2
    // IOException Thrown -3
    // No Files Could be Loaded -4
    // UnauthorizedAccessException Thrown -5

    public class VirtualFile
    {
        private const int FILEINDEXBYTES = 8;
        private const int FILESIZEBYTES = 8;
        private const int FILENAMEBYTES = 32;
        private const int MD5HASHSIZE = 16;
        private const int BYTESPERFILEINDEX = FILEINDEXBYTES + FILESIZEBYTES + FILENAMEBYTES + MD5HASHSIZE;

        public List<VirtualSubFile> SubFileList { get; private set; }
        private int CurrentID { get; set; }

        public VirtualFile()
        {
            SubFileList = new List<VirtualSubFile>();
            CurrentID = 0;
        }

        public int AddFile(string fileName)
        {
            try
            {
                int fileSize = (int)(new FileInfo(fileName)).Length;
                FileStream newFileStream = new FileStream(fileName, FileMode.Open);
                List<byte> newFileByteList = new List<byte>();
                for (int i = 0; i < fileSize; i++) newFileByteList.Add((byte)newFileStream.ReadByte());
                newFileStream.Close();

                newFileStream = new FileStream(fileName, FileMode.Open);
                byte[] file = new byte[fileSize];
                newFileStream.Read(file, 0, fileSize);
                newFileStream.Close();
                
                string fileNameWithoutDirectory = Path.GetFileName(fileName);
                VirtualSubFile subFile = new VirtualSubFile(CurrentID, fileNameWithoutDirectory);
                subFile.Data = newFileByteList.ToArray();
                SubFileList.Add(subFile);
                CurrentID++;
                return subFile.ID;
            }
            catch (FileNotFoundException)
            {
                return -1;
            }
            catch (FileLoadException)
            {
                return -2;
            }
            catch (UnauthorizedAccessException)
            {
                return -5;
            }
        }

        public int RemoveFileIndex(int index)
        {
            if (index > SubFileList.Count - 1) return -1;
            CurrentID = index;
            SubFileList.RemoveAt(index);
            for (int i = index; i < SubFileList.Count; i++) SubFileList[i].ID = i;
            return 0;
        }

        public int RemoveFileID(int ID)
        {
            int index = -1;
            for (int i = 0; i < SubFileList.Count; i++) if (SubFileList[i].ID == ID)
                {
                    index = i;
                    SubFileList.RemoveAt(i);
                    break;
                }
            for (int i = index; i < SubFileList.Count; i++) SubFileList[i].ID = i;
            return 0;
        }

        public int AddFolder(string folderName, bool includeSubDirectories = false)
        {
            string[] fileNames = Directory.GetFiles(folderName, "*.*", includeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            foreach (string fileName in fileNames) AddFile(fileName);
            return 0;
        }

        public int Build(string fileName)
        {
            try
            {
                List<byte> virtualFileDataList = new List<byte>();
                byte nFiles = (byte)SubFileList.Count();
                int totalIndexFileSize = BYTESPERFILEINDEX * nFiles;
                int totalManifestSize = totalIndexFileSize + 1;
                byte[] bytes = new byte[totalManifestSize];
                bytes[0] = nFiles;
                long currentStartPosition = totalManifestSize;
                for (int i = 0; i < SubFileList.Count; i++)
                {
                    long fileSize = SubFileList[i].Size;
                    currentStartPosition += fileSize;
                    int offset = 1;

                    //bytes[BYTESPERFILEINDEX * i + 0 + offset] = (byte)(currentStartPosition >> 56);
                    //bytes[BYTESPERFILEINDEX * i + 1 + offset] = (byte)(currentStartPosition >> 48);
                    //bytes[BYTESPERFILEINDEX * i + 2 + offset] = (byte)(currentStartPosition >> 40);
                    //bytes[BYTESPERFILEINDEX * i + 3 + offset] = (byte)(currentStartPosition >> 32);
                    //bytes[BYTESPERFILEINDEX * i + 4 + offset] = (byte)(currentStartPosition >> 24);
                    //bytes[BYTESPERFILEINDEX * i + 5 + offset] = (byte)(currentStartPosition >> 16);
                    //bytes[BYTESPERFILEINDEX * i + 6 + offset] = (byte)(currentStartPosition >> 8);
                    //bytes[BYTESPERFILEINDEX * i + 7 + offset] = (byte)(currentStartPosition);

                    //for (int a = 0; a < FILEINDEXBYTES; a++) bytes[BYTESPERFILEINDEX * i + a + offset] = (byte)(currentStartPosition >> ((FILEINDEXBYTES - 1) * 8) - (8 * a));

                    //bytes[BYTESPERFILEINDEX * i + 8 + offset] = (byte)(fileSize >> 56);
                    //bytes[BYTESPERFILEINDEX * i + 9 + offset] = (byte)(fileSize >> 48);
                    //bytes[BYTESPERFILEINDEX * i + 10 + offset] = (byte)(fileSize >> 40);
                    //bytes[BYTESPERFILEINDEX * i + 11 + offset] = (byte)(fileSize >> 32);
                    //bytes[BYTESPERFILEINDEX * i + 12 + offset] = (byte)(fileSize >> 24);
                    //bytes[BYTESPERFILEINDEX * i + 13 + offset] = (byte)(fileSize >> 16);
                    //bytes[BYTESPERFILEINDEX * i + 14 + offset] = (byte)(fileSize >> 8);
                    //bytes[BYTESPERFILEINDEX * i + 15 + offset] = (byte)(fileSize);

                    for (int a = 0; a < FILESIZEBYTES; a++) bytes[BYTESPERFILEINDEX * i + a + FILEINDEXBYTES + offset] = (byte)(fileSize >> ((FILESIZEBYTES - 1) * 8) - (8 * a));

                    offset = 17;

                    for (int a = 0; a < SubFileList[i].Name.Length; a++) bytes[BYTESPERFILEINDEX * i + a + offset] = (byte)(char)SubFileList[i].Name[a];

                    offset += FILENAMEBYTES;

                    for (int a = 0; a < SubFileList[i].MD5Hash.Length; a++) bytes[BYTESPERFILEINDEX * i + a + offset] = SubFileList[i].MD5Hash[a];
                }
                foreach (byte currentByte in bytes) virtualFileDataList.Add(currentByte);
                foreach (VirtualSubFile subFile in SubFileList) foreach (byte currentByte in subFile.Data) virtualFileDataList.Add(currentByte);
                byte[] virtualFileData = virtualFileDataList.ToArray();
                FileStream fileStream = new FileStream(fileName, FileMode.Create);
                foreach (byte currentByte in virtualFileData) fileStream.WriteByte(currentByte);
                fileStream.Close();
                return 0;
            }
            catch (FileNotFoundException)
            {
                return -1;
            }
            catch (FileLoadException)
            {
                return -2;
            }
            catch (IOException)
            {
                return -3;
            }
        }

        public int Load(string fileName)
        {
            try
            {
                FileStream fileStream = new FileStream(fileName, FileMode.Open);
                byte[] nFiles = new byte[1];
                fileStream.Read(nFiles, 0, 1);
                if (nFiles.Length > 0)
                {
                    SubFileList = new List<VirtualSubFile>();

                    for (int i = 0; i < (int)nFiles[0]; i++)
                    {
                        byte[] startIndexBytes = new byte[FILEINDEXBYTES];
                        fileStream.Read(startIndexBytes, 0, FILEINDEXBYTES);

                        long startPosition = (startIndexBytes[0] << 56) | (startIndexBytes[1] << 48) | (startIndexBytes[2] << 40) | (startIndexBytes[3] << 32) | (startIndexBytes[4] << 24) | (startIndexBytes[5] << 16) | (startIndexBytes[6] << 8) | (startIndexBytes[7]);

                        byte[] sizeBytes = new byte[FILESIZEBYTES];
                        fileStream.Read(sizeBytes, 0, FILESIZEBYTES);

                        long size = (sizeBytes[0] << 56) | (sizeBytes[1] << 48) | (sizeBytes[2] << 40) | (sizeBytes[3] << 32) | (sizeBytes[4] << 24) | (sizeBytes[5] << 16) | (sizeBytes[6] << 8) | (sizeBytes[7]);

                        byte[] fileNameBytes = new byte[FILENAMEBYTES];
                        fileStream.Read(fileNameBytes, 0, FILENAMEBYTES);
                        string currentFileName = "";
                        for (int a = 0; a < FILENAMEBYTES; a++) if (fileNameBytes[a] != 0x00) currentFileName += (char)fileNameBytes[a];
                        
                        byte[] md5Hash = new byte[MD5HASHSIZE];
                        fileStream.Read(md5Hash, 0, MD5HASHSIZE);
                        VirtualSubFile subFile = new VirtualSubFile(i);
                        subFile.Data = new byte[size];
                        subFile.MD5HashTarget = md5Hash;
                        subFile.Name = currentFileName;
                        SubFileList.Add(subFile);
                    }

                    int successfulLoads = 0;
                    for (int i = 0; i < (int)nFiles[0]; i++)
                    {
                        byte[] fileData = new byte[SubFileList[i].Size];
                        fileStream.Read(fileData, 0, (int)SubFileList[i].Size);
                        SubFileList[i].Data = fileData;
                        if (SubFileList[i].MD5Pass)
                        {
                            Console.BackgroundColor = ConsoleColor.Green;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.WriteLine("File '{0}' md5 pass...", SubFileList[i].Name);
                            Console.BackgroundColor = ConsoleColor.Black;
                            Console.ForegroundColor = ConsoleColor.White;
                            successfulLoads++;
                        }
                        else
                        {
                            Console.BackgroundColor = ConsoleColor.Red;
                            Console.WriteLine("File '{0}' md5 fail...", SubFileList[i].Name);
                            Console.BackgroundColor = ConsoleColor.Black;
                        }
                    }

                    fileStream.Close();
                    return successfulLoads;
                }
                fileStream.Close();
                return -4;
            }
            catch (FileNotFoundException)
            {
                return -1;
            }
            catch (FileLoadException)
            {
                return -2;
            }
        }

        public int CreateSubFiles(string directory, bool replaceIfExists, string filePrefix = "")
        {
            try
            {
                foreach (VirtualSubFile subFile in SubFileList)
                {
                    try
                    {
                        string fileName = String.Format("{0}{1}{2}", directory, filePrefix, subFile.Name);
                        if (File.Exists(fileName) && !replaceIfExists) fileName = String.Format("{0}vf_new_{1}{2}", directory, filePrefix, subFile.Name);
                        FileStream newFileStream = new FileStream(fileName, FileMode.Create);
                        foreach (byte currentByte in subFile.Data) newFileStream.WriteByte(currentByte);
                        newFileStream.Close();
                        Console.BackgroundColor = ConsoleColor.Green;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine("File '{0}' export succeeded...", subFile.Name);
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                        Console.WriteLine("File '{0}' export failed...", subFile.Name);
                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                }
                return 0;
            }
            catch (FileLoadException)
            {
                return -2;
            }
        }

        public class VirtualSubFile
        {
            public int ID;
            public string Name { get; set; }

            public VirtualSubFile(int id, string name = "")
            {
                ID = id;
                Name = name;
            }

            private byte[] data;
            public byte[] Data
            {
                get
                {
                    return data;
                }
                set
                {
                    data = value;
                    MD5Hash = MD5.Create().ComputeHash(value);
                    Size = data.Length;
                }
            }

            public bool MD5Pass { get { return Enumerable.SequenceEqual<byte>(MD5HashTarget, MD5Hash); } }
            public byte[] MD5HashTarget { get; set; }
            public byte[] MD5Hash { get; private set; }
            public long Size { get; private set; }
        }
    }

    static class HuffmanEncoder
    {
        public static byte[] Encode(byte[] data)
        {
            FrequencyAnalysis frequencyAnalysis = new FrequencyAnalysis(data);
            return data;
        }

        public static byte[] Decode(byte[] data)
        {
            return data;
        }

        class FrequencyAnalysis
        {
            public byte[] orderedByteValues;

            public FrequencyAnalysis(byte[] data)
            {
                byte[] byteValueFrequencies = new byte[256];
                for (int i = 0; i < byteValueFrequencies.Length; i++) byteValueFrequencies[i] = 0;
                for (int i = 0; i < data.Length; i++)byteValueFrequencies[data[i]]++;
                Console.WriteLine(BitConverter.ToString(byteValueFrequencies));

                KeyValuePair<byte, int>[] sorted = byteValueFrequencies.Select((x, i) => new KeyValuePair<byte, int>(x, i)).OrderBy(x => x.Key).ToArray();
                int[] orderedByteValueList = sorted.Select(x => x.Value).ToArray().Reverse().ToArray();
                orderedByteValues = new byte[orderedByteValueList.Length];
                for (int i = 0; i < orderedByteValues.Length; i++) orderedByteValues[i] = (byte)orderedByteValueList[i];
                Console.WriteLine(BitConverter.ToString(orderedByteValues));
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            VirtualFile virtualFile = new VirtualFile();

            string baseDirectory = @"";

            virtualFile.AddFolder(@"", true);

            Console.WriteLine("Building virtual file...");
            virtualFile.Build(baseDirectory + @"newfiles\VirtualFiles.vf");

            Console.WriteLine("Reading virtual file...");
            int successfulFiles = virtualFile.Load(baseDirectory + @"newfiles\VirtualFiles.vf");
            Console.WriteLine("Successfuly loaded {0} files...", successfulFiles);

            Console.WriteLine("Creating sub-files...");
            virtualFile.CreateSubFiles(baseDirectory + @"newfiles\", true);

            Console.WriteLine("Done");

            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
