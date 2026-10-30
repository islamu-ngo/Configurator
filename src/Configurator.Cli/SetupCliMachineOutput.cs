using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Configurator.Cli;

internal static class SetupCliMachineOutput
{
    internal static byte[] Serialize(SetupCliCommand command, SetupCliCommandResult result, int maximumOutputBytes)
    {
        string family = SetupCliCommandRegistry.Families.Any(item => item.Name == command.Family) ? command.Family : "cli";
        if (family == "doctor" && command.Operation != "doctor") family = "cli";
        string operation = SetupCliCommandRegistry.TryResolve(family, command.Operation, out _) ? command.Operation
            : "help";
        SetupCliMachineDiagnostic[] diagnostics = result.Diagnostics.Select(Sanitize).ToArray();
        string digest = DetailDigest(command, result, diagnostics);
        SetupCliMachineDetail? detail = HasDetailFailure(result) ? null : Detail(command, result, diagnostics, digest);
        SetupCliMachineDiagnostic[] inline = diagnostics.OrderByDescending(IsOperational).Take(16).ToArray();
        int inlineBudget = detail is null ? 8192 : 4096;
        while (inline.Sum(DiagnosticBytes) > inlineBudget) inline = inline[..^1];
        int budget = 8192 - inline.Sum(DiagnosticBytes);
        if (detail is not null)
        {
            detail = FitDetail(detail, budget);
            if (!detail.Terminal && detail.ReturnedCount == 0) throw new IOException("detail-item-unencodable");
        }
        string category = SetupCliResults.Lower(result.Exit);
        var envelope = new SetupCliMachineEnvelope("islamu-configurator-command/v4",
            new(family, operation, "machine"), category, category, (int)result.Exit, command.DryRun,
            inline, result.Artifacts, result.Coverage with { DetailDigest = digest }, result.Readiness)
        {
            Completion = result.Assessment is not null ? result.Completion
                : result.Completion == "cancelled" ? "cancelled" : result.Exit == SetupCliExitCode.Success ? "completed" : "failed",
            Preparation = result.Preparation,
            Cleanup = result.Cleanup,
            DiagnosticsTotal = diagnostics.Length,
            DiagnosticsOmitted = diagnostics.Length - inline.Length,
            DetailDigest = digest,
            Detail = detail,
            Assessment = result.Assessment,
            Help = result.Help,
            AuthoringContractVersion = family == "definition" && operation is "check" or "build" or "lint" && result.Exit == SetupCliExitCode.Success && result.Help is null ? 2 : null,
            RuntimeSchemaVersion = family == "definition" && operation is "check" or "build" or "lint" && result.Exit == SetupCliExitCode.Success && result.Help is null ? 2 : null,
            Schema = family == "definition" && operation == "schema" && result.Exit == SetupCliExitCode.Success && result.Help is null
                ? result.Schema : null
        };
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(envelope, SetupCliJsonContext.Default.SetupCliMachineEnvelope);
        if (body.Length + 1 > maximumOutputBytes) throw new IOException("output-bound");
        byte[] framed = new byte[body.Length + 1];
        body.CopyTo(framed, 0);
        framed[^1] = (byte)'\n';
        return framed;
    }

    private static int DiagnosticBytes(SetupCliMachineDiagnostic diagnostic) =>
        JsonSerializer.SerializeToUtf8Bytes(diagnostic, SetupCliJsonContext.Default.SetupCliMachineDiagnostic).Length;

    private static bool IsOperational(SetupCliMachineDiagnostic diagnostic) => diagnostic.Code is
        "io-failed" or "internal-failed" or "publication-failed" or "publication-cleanup-failed" or "publication-status-invalid"
        or "cancelled" or "detail-digest-mismatch";

    internal static SetupCliCommandResult ValidateDetail(SetupCliCommand command, SetupCliCommandResult result)
    {
        if (command.Detail is null) return result;
        if (command.Detail == "findings" && result.Assessment is null)
            return command.Operation == "compare" && result.Exit != SetupCliExitCode.Success ? result
                : SetupCliResults.Failure(SetupCliExitCode.Usage, "detail-kind-unavailable", "/detail");
        string digest = DetailDigest(command, result, result.Diagnostics.Select(Sanitize).ToArray());
        if (command.Offset > 0 && command.ExpectedDetailDigest != digest
            || command.ExpectedDetailDigest is not null && command.ExpectedDetailDigest != digest)
            return SetupCliResults.Failure(SetupCliExitCode.Data, "detail-digest-mismatch", "/detail");
        SetupCliMachineDetail detail = Detail(command, result, result.Diagnostics.Select(Sanitize).ToArray(), digest)!;
        if (detail.RetainedCount is < 0 or > 4096 || command.Offset > detail.RetainedCount)
            return SetupCliResults.Failure(SetupCliExitCode.Data, "detail-offset-invalid", "/detail");
        // Reserve the same inline budget as Serialize; all legal pages must make progress.
        int inlineBytes = result.Diagnostics.Select(Sanitize).OrderByDescending(IsOperational).Take(16).Sum(DiagnosticBytes);
        SetupCliMachineDetail fitted = FitDetail(detail, 8192 - Math.Min(4096, inlineBytes));
        return !fitted.Terminal && fitted.ReturnedCount == 0
            ? SetupCliResults.Failure(SetupCliExitCode.Data, "detail-item-unencodable", "/detail") : result;
    }

    private static bool HasDetailFailure(SetupCliCommandResult result) =>
        result.Diagnostics.Any(item => item.Code.StartsWith("detail-", StringComparison.Ordinal));

    private static SetupCliMachineDetail FitDetail(SetupCliMachineDetail detail, int budget)
    {
        while (JsonSerializer.SerializeToUtf8Bytes(detail, SetupCliJsonContext.Default.SetupCliMachineDetail).Length > budget
            && detail.ReturnedCount > 0)
            detail = detail.Findings.Count > 0 ? detail with { Findings = detail.Findings.SkipLast(1).ToArray() }
                : detail.Diagnostics.Count > 0 ? detail with { Diagnostics = detail.Diagnostics.SkipLast(1).ToArray() }
                : detail with { Coverage = detail.Coverage.SkipLast(1).ToArray() };
        return detail;
    }

    private static SetupCliMachineDetail? Detail(SetupCliCommand command, SetupCliCommandResult result,
        SetupCliMachineDiagnostic[] diagnostics, string digest)
    {
        if (command.Detail is null) return null;
        if (command.Detail == "findings")
            return result.Assessment is { } assessment
                ? new("findings", command.Offset, command.Limit, assessment.RetainedCount, assessment.Total, digest,
                    [], [], assessment.Findings.Skip(command.Offset).Take(command.Limit).ToArray()) : null;
        if (command.Detail == "diagnostics")
            return new("diagnostics", command.Offset, command.Limit, diagnostics.Length, diagnostics.Length, digest,
                diagnostics.Skip(command.Offset).Take(command.Limit).ToArray(), [], []);
        SetupCliMachineCoverageEntry[] entries = result.Coverage.CoveredKeys.Select(id => new SetupCliMachineCoverageEntry(id, "covered"))
            .Concat(result.Coverage.MissingKeys.Select(id => new SetupCliMachineCoverageEntry(id, "missing")))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.State, StringComparer.Ordinal).ToArray();
        return new("coverage", command.Offset, command.Limit, entries.Length, entries.Length, digest,
            [], entries.Skip(command.Offset).Take(command.Limit).ToArray(), []);
    }

    private static string DetailDigest(SetupCliCommand command, SetupCliCommandResult result, SetupCliMachineDiagnostic[] diagnostics)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Add(result.DefinitionDigest ?? "unassessed"); Add(command.Family); Add(command.Operation);
        foreach (string id in (command.Artifacts ?? (command.Artifact is null ? [] : [command.Artifact])).Order(StringComparer.Ordinal)) Add(id);
        foreach (SetupCliMachineDiagnostic diagnostic in diagnostics)
        {
            Add(diagnostic.Code); Add(diagnostic.Path); Add(diagnostic.Severity);
            Add(diagnostic.Line?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
            Add(diagnostic.Column?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
            Add(diagnostic.ByteOffset?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
            Add(diagnostic.Source is null ? "" : JsonSerializer.Serialize(diagnostic.Source, SetupCliJsonContext.Default.SetupCliMachineSourceLocation));
            Add(diagnostic.Correction is null ? "" : JsonSerializer.Serialize(diagnostic.Correction, SetupCliJsonContext.Default.SetupCliMachineCorrectionLocation));
        }
        foreach (string key in result.Coverage.CoveredKeys) { Add("covered"); Add(key); }
        foreach (string key in result.Coverage.MissingKeys) { Add("missing"); Add(key); }
        Add(result.Readiness.State);
        if (result.Assessment is { } assessment)
        {
            Add(JsonSerializer.Serialize(assessment, SetupCliJsonContext.Default.SetupCliMachineAssessment));
            foreach (SetupCliMachineFinding finding in assessment.Findings)
                Add(JsonSerializer.Serialize(finding, SetupCliJsonContext.Default.SetupCliMachineFinding));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
        void Add(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(bytes); hash.AppendData([0]);
        }
    }

    internal static SetupCliMachineDiagnostic Sanitize(SetupCliMachineDiagnostic diagnostic)
    {
        string pointer = diagnostic.Path.StartsWith("$.", StringComparison.Ordinal)
            ? "/" + diagnostic.Path[2..].Replace(".", "/", StringComparison.Ordinal) : diagnostic.Path == "$" ? "" : diagnostic.Path;
        if (pointer.Length > 256 || pointer.Any(char.IsControl) || pointer.Length > 0 && pointer[0] != '/') pointer = "";
        string code = diagnostic.Code.Length is > 0 and <= 96 && diagnostic.Code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? diagnostic.Code : "diagnostic-invalid";
        SetupCliMachineSourceLocation? source = diagnostic.Source;
        if (source is not null)
        {
            if (source.DocumentIndex is < 0 or > 4095 || source.StartLine < 1 || source.StartColumn < 1
                || source.EndLine < source.StartLine || source.EndColumn < 1
                || source.EndLine == source.StartLine && source.EndColumn < source.StartColumn) source = null;
            else
            {
                string path = source.SchemaPath;
                string root = path.Length == 0 ? "" : path[0] != '/' ? "invalid" : path.Split('/', 3)[1];
                bool knownRoot = root is "" or "$schema" or "schemaVersion" or "profile" or "identity" or "familyId"
                    or "revision" or "project" or "applicationVersions" or "entryPoints" or "locales" or "requiredCapabilities"
                    or "configuration" or "outputs" or "artifacts" or "presentation" or "localization" or "annotations";
                if (!knownRoot || path.Length > 0 && path[0] != '/') path = "";
                while (path.Length > 256 || path.Any(char.IsControl))
                {
                    int separator = path.LastIndexOf('/');
                    path = separator < 0 ? "" : path[..separator];
                }
                source = source with { SchemaPath = path };
            }
        }
        SetupCliMachineCorrectionLocation? correction = source is null ? diagnostic.Correction : null;
        if (correction is not null)
        {
            if (correction.InputRevision < 0 || correction.FieldId.Length > 128
                || correction.FieldId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')))
                correction = null;
            else
            {
                var segments = new List<SetupCliMachineLocationSegment>();
                int characters = 0;
                foreach (SetupCliMachineLocationSegment segment in correction.Segments.Take(32))
                {
                    if (segment.Kind == "property" && segment.PropertyName is { } name && segment.Index is null
                        && name.Length <= 256 - characters)
                    {
                        characters += name.Length;
                        segments.Add(segment);
                    }
                    else if (segment.Kind is "arrayRow" or "mapEntry" && segment.PropertyName is null
                        && segment.Index is >= 0 and <= 4095) segments.Add(segment);
                    else break;
                }
                correction = correction with { Segments = segments.AsReadOnly() };
            }
        }
        return diagnostic with
        {
            Code = code,
            Path = pointer,
            Severity = diagnostic.Severity is "error" or "warning" or "info" ? diagnostic.Severity : "error",
            Line = diagnostic.Line > 0 ? diagnostic.Line : null,
            Column = diagnostic.Column > 0 ? diagnostic.Column : null,
            ByteOffset = diagnostic.ByteOffset >= 0 ? diagnostic.ByteOffset : null,
            Source = source,
            Correction = correction
        };
    }
}
