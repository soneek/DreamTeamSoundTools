using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DTSoundData
{
    public class SoundDataArc
    {
        public string path;
        public Dictionary<int, SoundDataArcTable> tables = new Dictionary<int, SoundDataArcTable>();
        public Dictionary<int, string> soundTypes = new Dictionary<int, string>
        {
            { 0, "WAVE" },
            { 1, "SE" },
            { 2, "SEB" },
            { 4, "STRSE" },
            { 5, "STRBGM" },
            { 6, "STRVOICE" }
        };

        public SoundDataArc(string filename)
        {
            path = filename;
            parseArchive();
        }

        private void parseArchive()
        {
            if (File.Exists(path))
            {

                try
                {
                    var arcSize = new FileInfo(path).Length;
                    using (BinaryReader b = new BinaryReader(File.Open(path, FileMode.Open)))
                    {
                        while (b.BaseStream.Position < arcSize)
                        {
                            var tableID = b.ReadInt32();
                            tables.Add(tableID, new SoundDataArcTable());
                            tables[tableID].entryCount = b.ReadInt32();
                            tables[tableID].tableSize = b.ReadInt32();
                            tables[tableID].relFileDataOffset = b.ReadInt32();
                            tables[tableID].tableStartOffset = b.ReadInt32();
                            tables[tableID].stringTableSize = b.ReadInt32();

                            // Getting file sizes and offsets
                            for (int i = 0; i < tables[tableID].entryCount; i++)
                            {
                                b.BaseStream.Seek(tables[tableID].tableStartOffset + 0x20 + i * 0x10, SeekOrigin.Begin);

                                tables[tableID].fileList.Add(i, new SoundDataFile());
                                tables[tableID].fileList[i].fileID = b.ReadInt32();
                                tables[tableID].fileList[i].size = b.ReadInt32();
                                tables[tableID].fileList[i].offset = b.ReadInt32();
                            }
                            b.BaseStream.Seek(tables[tableID].tableStartOffset + 0x20 + tables[tableID].entryCount * 0x10, SeekOrigin.Begin);
                            // Getting file names
                            for (int i = 0; i < tables[tableID].entryCount; i++)
                            {
                                tables[tableID].fileList[i].name = b.ReadString().TrimEnd('\x00');
                            }
                            b.BaseStream.Seek(tables[tableID].tableStartOffset + tables[tableID].tableSize, SeekOrigin.Begin);
                        }
                        b.Close();
                    }
                }
                catch (FileNotFoundException ex)
                {
                    Debug.Write(ex);
                }

            }
        }

        public void extractArchive(string outFolder)
        {
            if (!Directory.Exists(outFolder))
                Directory.CreateDirectory(outFolder);

            try
            {
                using (BinaryReader b = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    // Extract each table
                    foreach (KeyValuePair<int, SoundDataArcTable> table in tables)
                    {
                        string tableFolder = Path.Combine(outFolder, soundTypes[table.Key]);
                        if (!Directory.Exists(tableFolder))
                            Directory.CreateDirectory(tableFolder);

                        // Extract individual files
                        foreach (KeyValuePair<int, SoundDataFile> sFile in table.Value.fileList)
                        {
                            b.BaseStream.Seek(table.Value.tableStartOffset + sFile.Value.offset, SeekOrigin.Begin);
                            string outFile = Path.Combine(tableFolder, sFile.Value.name + ".rsd");
                            BinaryWriter writer = new BinaryWriter(File.Open(outFile, FileMode.Create));
                            writer.Write(b.ReadBytes(sFile.Value.size));
                            writer.Close();
                        }
                    }
                    b.Close();
                }
            }
            catch (FileNotFoundException ex)
            {
                Debug.Write(ex);
            }
            
               
        }

        public bool buildArchive(string sourceFolder, string newSoundArchive)
        {
            if (!Directory.Exists(sourceFolder))
            {
                Debug.WriteLine("Source directory {0} does not exist!", sourceFolder);
                return false;
            }

            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Open(newSoundArchive, FileMode.Create)))
                {

                    int currentOffset = 0;
                    // Write each table
                    foreach (KeyValuePair<int, SoundDataArcTable> table in tables)
                    {
                        table.Value.tableSize = table.Value.relFileDataOffset;
                        int fileOffset = table.Value.relFileDataOffset;
                        string tableFolder = Path.Combine(sourceFolder, soundTypes[table.Key]);
                        if (!Directory.Exists(tableFolder))
                        {
                            Debug.WriteLine("Table directory {0} does not exist!", tableFolder);
                            writer.Close();
                            return false;
                        }
                        writer.Write(table.Key);
                        writer.Write(table.Value.entryCount);
                        var tempPos = writer.BaseStream.Position;

                        // Getting file sizes, and calculating table size
                        foreach (KeyValuePair<int, SoundDataFile> sFile in table.Value.fileList)
                        {
                            sFile.Value.size = Convert.ToInt32(new FileInfo(Path.Combine(tableFolder, sFile.Value.name + ".rsd")).Length);
                            sFile.Value.offset = fileOffset;
                            table.Value.tableSize += sFile.Value.size;
                            fileOffset += sFile.Value.size;
                        }

                        writer.Write(table.Value.tableSize);
                        writer.Write(table.Value.relFileDataOffset);
                        table.Value.tableStartOffset = currentOffset;
                        writer.Write(currentOffset);
                        writer.Write(table.Value.stringTableSize);
                        writer.Write(0);
                        writer.Write(0);

                        // Writing file metadata
                        foreach (KeyValuePair<int, SoundDataFile> sFile in table.Value.fileList)
                        {                            
                            writer.Write(sFile.Value.fileID);
                            writer.Write(sFile.Value.size);
                            writer.Write(sFile.Value.offset);
                            writer.Write(0);
                        }

                        // Writing file names
                        foreach (KeyValuePair<int, SoundDataFile> sFile in table.Value.fileList)
                        {
                            writer.Write(Convert.ToByte(sFile.Value.name.Length+1));
                            writer.Write(Encoding.ASCII.GetBytes(sFile.Value.name + '\x00'));                                                    
                        }

                        while (writer.BaseStream.Position < table.Value.relFileDataOffset + table.Value.tableStartOffset)
                            writer.Write(Convert.ToByte(0));

                        // Writing file data
                        foreach (KeyValuePair<int, SoundDataFile> sFile in table.Value.fileList)
                        {
                            writer.Write(File.ReadAllBytes(Path.Combine(tableFolder, sFile.Value.name + ".rsd")));
                        }

                        // Updating absolute offset in overall archive
                        currentOffset += table.Value.tableSize;
                    }
                    writer.Close();
                }

                
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }


            return true;                
        }
    }
   
    public class SoundDataArcTable
    {
        public int entryCount;
        public int tableSize;
        public int relFileDataOffset;
        public int tableStartOffset;
        public int stringTableSize;
        public Dictionary<int, SoundDataFile> fileList = new Dictionary<int, SoundDataFile>();
    }

    public class SoundDataFile
    {
        public int fileID;
        public int size;
        public int offset;
        public string name;
    }
}
