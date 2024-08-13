using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Core.Audio
{
    public class AudioProcessor
    {
        public static void CombineMP3Files(IEnumerable<string> inputFiles, string outputFile)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var outputWriter = new WaveFileWriter(outputStream, new WaveFormat()))
                {
                    foreach (var inputFile in inputFiles)
                    {
                        using (var inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                        {
                            using (var mp3Reader = new Mp3FileReader(inputStream))
                            {
                                mp3Reader.CopyTo(outputWriter);
                            }
                        }
                    }
                }

                File.WriteAllBytes(outputFile, outputStream.ToArray());
            }
        }


        public static void MergeMP3Files(List<string> inputFiles, string outputFile, Action<double> callBack)
        {
            using (var output = new MemoryStream())
            {
                int index = 0;
                foreach (string file in inputFiles)
                {
                    Mp3FileReader reader = new Mp3FileReader(file);
                    if ((output.Position == 0) && (reader.Id3v2Tag != null))
                    {
                        output.Write(reader.Id3v2Tag.RawData, 0, reader.Id3v2Tag.RawData.Length);
                    }
                    Mp3Frame frame;
                    while ((frame = reader.ReadNextFrame()) != null)
                    {
                        output.Write(frame.RawData, 0, frame.RawData.Length);
                    }
                    callBack(Math.Round(((double)index / inputFiles.Count) * 100, 2));
                    index++;
                }
                callBack(100);
                File.WriteAllBytes(outputFile, output.ToArray());
            }
        }

        private static void CopyAudioData(Mp3FileReader inputReader, WaveFileWriter outputWriter)
        {
            byte[] buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = inputReader.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputWriter.Write(buffer, 0, bytesRead);
            }
        }

        public static List<string> GetFileNames(string inputFolder)
        {
            if (!string.IsNullOrEmpty(inputFolder))
            {
                string[] fileNames = Directory.GetFiles(inputFolder);
                return fileNames.Select(f => Path.Combine(inputFolder, Path.GetFileName(f))).ToList();
            }
            return new List<string>();
        }

        public static bool ExistFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                return true;
            }

            return false;
        }
    }
}
