using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SourceGit.Commands
{
    /// <summary>
    ///     A C# version of https://github.com/anjerodev/commitollama
    /// </summary>
    public class GenerateCommitMessage
    {
        public class GetDiffContent : Command
        {
            public GetDiffContent(string repo, Models.DiffOption opt)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = $"diff --diff-algorithm=minimal {opt}";
            }
        }

        public GenerateCommitMessage(Models.OpenAIService service, string repo, List<Models.Change> changes, CancellationToken cancelToken, Action<string> onProgress, Action<string> onResponse)
        {
            _service = service;
            _repo = repo;
            _changes = changes;
            _cancelToken = cancelToken;
            _onProgress = onProgress;
            _onResponse = onResponse;
        }

        public void Exec()
        {
            try
            {
                var summaryBuilder = new StringBuilder();
                var bodyBuilder = new StringBuilder();
                _onResponse?.Invoke("Wait for all file analysis to complete...");
                foreach (var change in _changes)
                {
                    if (_cancelToken.IsCancellationRequested)
                        return;

                    _onProgress?.Invoke($"Analyzing {change.Path}...");
                    _onResponse?.Invoke($"Wait for all file analysis to complete...\n\n{bodyBuilder}");

                    bodyBuilder.Append("- ");
                    summaryBuilder.Append("- ");
                    GenerateChangeSummary(change, summaryBuilder, bodyBuilder);

                    bodyBuilder.Append("\n");
                    summaryBuilder.Append("(file: ");
                    summaryBuilder.Append(change.Path);
                    summaryBuilder.Append(")\n");
                }

                if (_cancelToken.IsCancellationRequested)
                    return;

                _onProgress?.Invoke($"Generating commit message...");
                var body = bodyBuilder.ToString();
                GenerateSubject(summaryBuilder.ToString(), body);
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, $"Failed to generate commit message: {e}");
            }
        }

        private void GenerateChangeSummary(Models.Change change, StringBuilder summary, StringBuilder body)
        {
            var rs = new GetDiffContent(_repo, new Models.DiffOption(change, false)).ReadToEnd();
            var diff = rs.IsSuccess ? rs.StdOut : "unknown change";

            _service.Chat(_service.AnalyzeDiffPrompt, $"Here is the `git diff` output: {diff}", _cancelToken, update =>
            {
                body.Append(update);
                summary.Append(update);
                _onResponse?.Invoke($"Wait for all file analysis to complete...\n\n{body}");
            });
        }

        private void GenerateSubject(string summary, string body)
        {
            StringBuilder result = new StringBuilder();
            _service.Chat(_service.GenerateSubjectPrompt, $"Here are the summaries changes:\n{summary}", _cancelToken, update =>
            {
                result.Append(update);
                _onResponse?.Invoke($"{result}\n\n{body}");
            });
        }

        private Models.OpenAIService _service;
        private string _repo;
        private List<Models.Change> _changes;
        private CancellationToken _cancelToken;
        private Action<string> _onProgress;
        private Action<string> _onResponse;
    }
}
