using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static XIVLauncher.Common.Http.OtpListener;

namespace XIVLauncher.Common.Shell
{
    public class OtpShellReceiver
    {
        private readonly string fileName;
        private readonly string args;


        /// <summary>
        /// Create a shell command receiver that takes the given configured shell command
        /// and executes it to derive the OTP
        /// </summary>
        /// <param name="shellCommand">The configured command. The first space position is treated as the separator between filename and args</param>
        public OtpShellReceiver(string shellCommand)
        {
            // Parse shell command. Just get the first part and then the arguments afterward
            var firstSpacePosition = shellCommand.IndexOf(" ");

            if (firstSpacePosition == -1)
            {
                this.fileName = shellCommand;
                this.args = string.Empty;
            } else
            {
                this.fileName = shellCommand.Substring(0, firstSpacePosition);
                this.args = shellCommand.Substring(firstSpacePosition + 1);
            }
        }

        public async Task<(bool wasGotten, string? oneTimePassword)> TryGetOneTimePasswordAsync(CancellationToken cts = default)
        {
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.fileName, // "op",
                    Arguments = this.args, // "item get jxtm37qvd5f6tjezazg6grwq7u --otp",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            using var _ = cts.Register(() =>
            {
                try
                {
                    proc.Kill();
                }
                catch
                {

                }
            });

            try
            {
                var pass = await Task.Run(() =>
                {
                    if (this.TryGetOneTimePassword(proc, out string otp))
                    {
                        return otp;
                    } else
                    {
                        return null;
                    }
                });

                if (pass == null)
                {
                    return (false, null);
                }
                return (true, pass);
            } catch
            {
                return (false, null);
            }
        }

        public bool TryGetOneTimePassword(Process proc, out String? otp)
        {
            proc.Start();
            var builder = new StringBuilder();
            while (!proc.StandardOutput.EndOfStream)
            {
                builder.AppendLine(proc.StandardOutput.ReadLine());
            }


            if (proc.ExitCode != 0)
            {
                otp = null;
                return false;
            }

            var result = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                otp = null;
                return false;
            }

            otp = result;
            return true;

        }
    }
}
