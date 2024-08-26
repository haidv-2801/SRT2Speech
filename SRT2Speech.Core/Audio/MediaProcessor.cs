using AutoApp.Utility;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Primitives;
using SRT2Speech.Core.Models;
using SRT2Speech.Core.Utilitys;
using SubtitlesParser.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SRT2Speech.Core.Audio
{
    public class MediaProcessor
    {
        private readonly string _audioFolder;
        private readonly string _videoPath = @"D:\tool\sample.mp4";
        private readonly string _srtPath;
        private readonly IEnumerable<SubtitleItem> _subTitles;
        private readonly double _cutTime = 250;
        private string _outMp3Folder = @"D:\tool\SRT2Speech\SRT2Speech\SRT2Speech.AppWindow\bin\Debug\net8.0-windows\Files\English\Cutted";

        public MediaProcessor(string audioFolder, string videoPath, string srtPath)
        {
            _audioFolder = audioFolder;
            _videoPath = videoPath;
            _srtPath = srtPath;
            _subTitles = GetSubtitles();
        }

        public IEnumerable<SubtitleItem> GetDataSubtitles() { return _subTitles; }

        public async Task CompressAudioBySubtitles(Action<string> callback)
        {
            bool isCreateOut = false;
            string outMp3s = "";
            var mp3s = GetAllMp3Path();
            double newStartMs = 0;
            double newEndMs = 0;

            foreach (var subtitle in _subTitles)
            {
                var mp3Path = mp3s.FirstOrDefault(f => f == $"{Path.GetDirectoryName(f)}\\{subtitle.Index}.mp3");
                if (mp3Path != null)
                {
                    var actualDuration = await FFmpegFluent.Builder.Create().RunFfprobeAsync($"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {mp3Path}");
                    double.TryParse(actualDuration.Data, out double _dur);
                    double actualDurationMs = Math.Max(_dur * 1000 - _cutTime, 0);

                    double fromMs = subtitle.StartTime;
                    double endMs = subtitle.EndTime;

                    if (!isCreateOut)
                    {
                        outMp3s = $"{Path.GetDirectoryName(mp3Path)!}/Cutted";
                        _outMp3Folder = outMp3s;
                        CreateFolderIfNotExist(outMp3s);
                        isCreateOut = true;
                    }

                    //cắt video và lưu lại để tí speed up/down
                    await Cli.Wrap("ffmpeg")
                        .WithArguments($@"-y -ss {subtitle.StartTimeSpanShort} -to {subtitle.EndTimeSpanShort} -i {_videoPath} -c copy {outMp3s}/outvideo_{subtitle.Index}.mp4")
                        .ExecuteAsync();

                    double targetDurationMs = endMs - fromMs;
                    if (actualDurationMs == 0)
                    {
                        actualDurationMs = targetDurationMs;
                    }

                    double slowingFactor = (double)actualDurationMs / targetDurationMs;

                    //Sử lí video vừa cắt xong theo slowingFactor
                    await Cli.Wrap("ffmpeg")
                        .WithArguments($@"-y -i {outMp3s}/outvideo_{subtitle.Index}.mp4 -vf ""setpts={slowingFactor}*PTS"" {outMp3s}/slow/outvideo_{subtitle.Index}_slow.mp4")
                        .ExecuteAsync();

                    newEndMs = actualDurationMs;

                    //gán lại data mới
                    subtitle.SlowingFactor = slowingFactor;
                    subtitle.StartTime = (int)newStartMs;
                    subtitle.EndTime = (int)newStartMs + (int)newEndMs;

                    newStartMs = actualDurationMs;
                }
            }
        }

        public async Task<bool> MergeAudio(Action<string> callback)
        {
            if (string.IsNullOrEmpty(this._outMp3Folder))
            {
                throw new DirectoryNotFoundException($"[{this._outMp3Folder}] not found");
            }

            var mp4s = GetAllProcessedMp3Path();
            mp4s = mp4s.OrderBy(x =>
            {
                string input = x;
                int startIndex = input.IndexOf("_") + 1;
                int endIndex = input.IndexOf("_", startIndex);
                int extractedNumber = int.Parse(input.Substring(startIndex, endIndex - startIndex));
                return extractedNumber;
            });

            try
            {
                using (FileStream fileStream = new FileStream(Path.Combine(this._outMp3Folder, "slow_mp4.txt"), FileMode.Create, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(fileStream))
                {
                    writer.Write(string.Join("\n", mp4s.Select(f => $"file '{f}'")));
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"An error occurred while writing to the file: {ex.Message}");
            }
            string command = $"-y -f concat -safe 0 -i {Path.Combine(this._outMp3Folder, "/slow/slow_mp4.txt")} -c copy {Path.Combine(this._outMp3Folder, "output_final_slow.mp4")}";

            var cmd = Cli.Wrap("ffmpeg").WithArguments(command).WithValidation(CommandResultValidation.None);

            await foreach (var cmdEvent in cmd.ListenAsync())
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        callback($"Process started; ID: {started.ProcessId}");
                        break;
                    case StandardOutputCommandEvent stdOut:
                        callback($"Out> {stdOut.Text}");
                        break;
                    case StandardErrorCommandEvent stdErr:
                        callback($"Err> {stdErr.Text}");
                        break;
                    case ExitedCommandEvent exited:
                        callback($"Process exited; Code: {exited.ExitCode}");
                        break;
                }
            }
            return true;
        }

        public IEnumerable<SubtitleItem> GetSubtitles()
        {
            if (!ExistFile(_srtPath))
            {
                throw new FileNotFoundException($"Không tồn tại file {_srtPath}");
            }
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using (var fileStream = File.OpenRead(_srtPath))
            {
                var items = parser.ParseStream(fileStream, Encoding.UTF8);
                return items;
            }
        }

        public IEnumerable<string> GetAllMp3Path()
        {
            Matcher matcher = new();
            matcher.AddIncludePatterns(new[] { "*.mp3" });
            return matcher.GetResultsInFullPath(_audioFolder);
        }

        public IEnumerable<string> GetAllProcessedMp3Path()
        {
            Matcher matcher = new();
            matcher.AddIncludePatterns(new[] { "outvideo_*_slow.mp4" });
            return matcher.GetResultsInFullPath(this._outMp3Folder);
        }

        public bool ExistFolder(string path)
        {
            return Directory.Exists(path);
        }

        public void CreateFolderIfNotExist(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public bool ExistFile(string path)
        {
            return File.Exists(path);
        }
    }
}
