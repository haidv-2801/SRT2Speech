using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegFluent
{
    /// <summary>
    /// <see cref="https://ffmpeg.org/"/>
    /// </summary>
    internal class FFmpeg
    {
        internal bool AddLocalExecutable { get; set; }
        internal string GlobalOptions { get; set; }
        internal string InputFileOptions { get; set; }
        internal string InputFile { get; set; }
        internal string OutputFileOptions { get; set; }
        internal string OutputFile { get; set; }

        private readonly string LocalFfmpeg = @"ffmpeg";
        private readonly string LocalFfprobe = @"ffprobe";

        public async Task<CommandResult> RunFfmpegAsync()
        {
            this.AddLocalExecutable = false;
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();
            var args = this.ToString();
            var cli = Cli.Wrap(LocalFfmpeg).WithArguments(args)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer));
            var res = await cli.ExecuteAsync();
            return new CommandResult
            {
                Success = res.IsSuccess,
                Data = stdOutBuffer.ToString(),
                Error = stdErrBuffer.ToString()
            };
        }

        public async Task<CommandResult> RunFfmpegAsync(string command)
        {
            this.AddLocalExecutable = false;
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();
            var cli = Cli.Wrap(LocalFfmpeg).WithArguments(command)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer));
            var res = await cli.ExecuteBufferedAsync();
            return new CommandResult
            {
                Success = res.IsSuccess,
                Data = stdOutBuffer.ToString(),
                Error = stdErrBuffer.ToString()
            };
        }


        public async Task<CommandResult> RunFfprobeAsync(string command)
        {
            this.AddLocalExecutable = false;
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();
            var cli = Cli.Wrap(LocalFfprobe).WithArguments(command)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer));
            var res = await cli.ExecuteAsync();
            return new CommandResult
            {
                Success = res.IsSuccess,
                Data = stdOutBuffer.ToString(),
                Error = stdErrBuffer.ToString()
            };
        }

        public async Task<CommandResult> RunFfprobeAsync()
        {
            this.AddLocalExecutable = false;
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();
            string command = this.ToString();
            var cli = Cli.Wrap(LocalFfprobe).WithArguments(command)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer));
            var res = await cli.ExecuteAsync();
            return new CommandResult
            {
                Success = res.IsSuccess,
                Data = stdOutBuffer.ToString(),
                Error = stdErrBuffer.ToString()
            };
        }

        public override string ToString()
        {
            // ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url} ... 
            StringBuilder sb = new StringBuilder();
            if (AddLocalExecutable)
                sb.Append($@" ffmpeg");

            if (!string.IsNullOrEmpty(GlobalOptions))
                sb.Append($@" {GlobalOptions}");

            if (!string.IsNullOrEmpty(InputFileOptions))
                sb.Append($@" {InputFileOptions}");

            if (!string.IsNullOrEmpty(InputFile))
                sb.Append($@" -i {InputFile}");

            if (!string.IsNullOrEmpty(OutputFileOptions))
                sb.Append($@" {OutputFileOptions}");

            if (!string.IsNullOrEmpty(OutputFile))
                sb.Append($@" ""{OutputFile}""");

            return sb.ToString().Trim();
        }
    }
}
