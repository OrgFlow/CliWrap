﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CliWrap.Internal
{
    internal class ProcessEx : IDisposable
    {
        private readonly Process _nativeProcess;

        private readonly TaskCompletionSource<object?> _exitTcs =
            new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Id { get; private set; }

        // We are purposely using Stream instead of StreamWriter/StreamReader to push the concerns of
        // writing and reading to PipeSource/PipeTarget at the higher level.

        public Stream StdIn { get; private set; } = Stream.Null;

        public Stream StdOut { get; private set; } = Stream.Null;

        public Stream StdErr { get; private set; } = Stream.Null;

        public int ExitCode { get; private set; }

        public DateTimeOffset StartTime { get; private set; }

        public DateTimeOffset ExitTime { get; private set; }

        public ProcessEx(ProcessStartInfo startInfo)
        {
            _nativeProcess = new Process {StartInfo = startInfo};
        }

        public void Start()
        {
            _nativeProcess.EnableRaisingEvents = true;
            _nativeProcess.Exited += (sender, args) =>
            {
                ExitTime = DateTimeOffset.Now;
                ExitCode = _nativeProcess.ExitCode;
                _exitTcs.TrySetResult(null);
            };

            if (!_nativeProcess.Start())
            {
                throw new InvalidOperationException(
                    "Failed to obtain the handle when starting a process. " +
                    "This could mean that the target executable doesn't exist or that execute permission is missing."
                );
            }

            StartTime = DateTimeOffset.Now;

            Id = _nativeProcess.Id;
            StdIn = _nativeProcess.StartInfo.RedirectStandardInput ? _nativeProcess.StandardInput.BaseStream : StdIn;
            StdOut = _nativeProcess.StartInfo.RedirectStandardOutput ? _nativeProcess.StandardOutput.BaseStream : StdOut;
            StdErr = _nativeProcess.StartInfo.RedirectStandardError ? _nativeProcess.StandardError.BaseStream : StdErr;
        }

        public bool TryKill()
        {
            try
            {
                _nativeProcess.EnableRaisingEvents = false;
                _nativeProcess.Kill(true);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _exitTcs.TrySetCanceled();
            }
        }

        public async Task WaitUntilExitAsync() => await _exitTcs.Task;

        public void Dispose()
        {
            // Kill the process if it's still alive by this point
            if (!_nativeProcess.HasExited)
                TryKill();

            _nativeProcess.Dispose();
        }
    }
}