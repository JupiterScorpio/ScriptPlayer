using System;
using System.Globalization;

namespace ScriptPlayer.Shared
{
    public class ClipExtractorArguments : FfmpegArguments
    {
        public int Height { get; set; }

        public int Width { get; set; }

        public double Framerate { get; set; }

        public TimeSpan Duration { get; set; }

        public TimeSpan StartTimeSpan { get; set; }

        public string OutputFile { get; set; }

        public bool ClipLeft { get; set; }

        public bool DeLense { get; set; }

        public override string BuildArguments()
        {
            string framerate = Framerate.ToString("F2", CultureInfo.InvariantCulture);

            return
                "-y " + //Yes to override existing files
                $"-ss {StartTimeSpan:hh\\:mm\\:ss\\.ff} " + // Starting Position
                $"-i \"{InputFile}\" " + // Input File
                $"-t {Duration:hh\\:mm\\:ss\\.ff} " + // Duration
                $"-r {framerate} " +
                "-vf " + // video filter parameters" +
                $"\"" + 
                $"setpts=PTS-STARTPTS, hqdn3d=10"+ 
                (ClipLeft ? $", stereo3d=sbsl:ml" : "") + // de-3D
                // (DeLense ? $", lenscorrection=k1=0:k2=-0.33" : "") +
                (ClipLeft ? $", scale = {Width / 2}:{Height}" : $", scale = {Width}:{Height}") + 
                "\" " +
                (ClipLeft ? "-metadata:s:v:0 stereo_mode=\"mono\" " : "") + //de-3D
                "-vcodec libx264 -crf 0 " +
                $"\"{OutputFile}\"";
        }
    }
}