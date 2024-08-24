using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegFluent
{
    public class Builder
    {
        readonly FFmpeg ffmped = new FFmpeg();

        public static Builder Create()
        {
            return new Builder();
        }

        public Builder AddLocalExecutable()
        {
            ffmped.AddLocalExecutable = true;
            return this;
        }

        public Builder GlobalOptions(string globalOptions)
        {
            ffmped.GlobalOptions = globalOptions;
            return this;
        }

        public Builder InputFileOptions(string inputFileOptions)
        {
            ffmped.InputFileOptions = inputFileOptions;
            return this;
        }

        public Builder InputFiles(IEnumerable<string> inputFiles)
        {
            ffmped.InputFile = String.Join(" -i ", inputFiles.Select(f => $@"""{f}"""));
            return this;
        }

        public Builder OutputFileOptions(string inputFileOptions)
        {
            ffmped.OutputFileOptions = inputFileOptions;
            return this;
        }

        public Builder OutputFile(string outputFile)
        {
            ffmped.OutputFile = outputFile;
            return this;
        }

        public override string ToString()
        {
            return ffmped.ToString();
        }

        public async Task<CommandResult> RunFfmpegAsync()
        {
            return await ffmped.RunFfmpegAsync();
        }

        public async Task<CommandResult> RunFfmpegAsync(string cmd)
        {
            return await ffmped.RunFfmpegAsync(cmd);
        }

        public async Task<CommandResult> RunFfprobeAsync(string command)
        {
            return await ffmped.RunFfprobeAsync(command);
        }
    }
}
