using System;
using System.IO;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;          // ExceptionOperations
using Microsoft.Scripting;                  // SourceCodeKind

namespace Swarm.Client.Core
{
    public static class PythonRunner
    {
        public static (bool ok, string resultB64, string error) RunPy2(string pythonText)
        {
            ScriptEngine engine; // no unnecessary assignment
            try
            {
                engine = Python.CreateEngine();   // IronPython 2.x
                var scope = engine.CreateScope();

                // Optional: add stdlib search path if "Lib" is next to the EXE
                try
                {
                    var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    var libPath = Path.Combine(exeDir, "Lib");
                    if (Directory.Exists(libPath))
                    {
                        var paths = engine.GetSearchPaths();
                        paths.Add(libPath);
                        engine.SetSearchPaths(paths);
                    }
                }
                catch { /* non-fatal */ }

                using (var outMs = new MemoryStream())
                using (var errMs = new MemoryStream())
                using (var outSw = new StreamWriter(outMs, Encoding.UTF8) { AutoFlush = true })
                using (var errSw = new StreamWriter(errMs, Encoding.UTF8) { AutoFlush = true })
                {
                    engine.Runtime.IO.SetOutput(outMs, outSw);
                    engine.Runtime.IO.SetErrorOutput(errMs, errSw);

                    var src = engine.CreateScriptSourceFromString(pythonText, SourceCodeKind.Statements);

                    try
                    {
                        src.Execute(scope);

                        string resultText = null;
                        if (scope.ContainsVariable("result"))
                        {
                            var obj = scope.GetVariable("result");
                            resultText = obj != null ? obj.ToString() : string.Empty;
                        }

                        outSw.Flush();
                        errSw.Flush();

                        var stdoutRaw = Encoding.UTF8.GetString(outMs.ToArray()) ?? string.Empty;
                        var stderrRaw = Encoding.UTF8.GetString(errMs.ToArray());

                        var stderr = string.IsNullOrWhiteSpace(stderrRaw) ? null : stderrRaw.TrimEnd('\r', '\n');
                        var output = (resultText ?? stdoutRaw).TrimEnd('\r', '\n');

                        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(output ?? string.Empty));
                        return (stderr == null, b64, stderr);
                    }
                    catch (Exception ex)
                    {
                        outSw.Flush();
                        errSw.Flush();

                        var stdout = (Encoding.UTF8.GetString(outMs.ToArray()) ?? string.Empty).TrimEnd('\r', '\n');
                        var outputB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(stdout));

                        string friendly;
                        try
                        {
                            var eo = engine.GetService<ExceptionOperations>();
                            friendly = eo != null ? eo.FormatException(ex) : ex.Message;
                        }
                        catch
                        {
                            friendly = ex.Message;
                        }

                        friendly = (friendly ?? string.Empty).Replace("\r\n", "\n").TrimEnd();
                        if (string.IsNullOrWhiteSpace(friendly)) friendly = "Unhandled script error";

                        return (false, outputB64, friendly);
                    }
                }
            }
            catch (Exception exOuter)
            {
                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Empty));
                return (false, b64, exOuter.Message);
            }
        }
    }
}
