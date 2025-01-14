using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

using Spider.Lib;
using Spider.Lib.FileLib;
using Spider.Lib.JsonLib;

namespace Spider {
    static class Program {
        static async Task Main(string[] args) {
            //Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException());

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            var cfg = (await JsonReader.ReadConfigAsync())[0];
            var parser = new InfoParser(cfg.Configuration, cfg.CustomConfigurations);
            var root = new DirectoryInfo(
                $"{Directory.GetCurrentDirectory()}\\projects\\{cfg.Version}\\assets");

            var names = root.GetDirectories().Select(_ => _.Name).ToList();

            foreach (var configuration in cfg.CustomConfigurations) {
                if (!names.Contains(configuration.ProjectName)) {
                    names.Add(configuration.ProjectName);
                }
            }

            var allM = await UrlLib.GetModInfoAsync(cfg.Count, cfg.Version);
            var allN = allM.ToList().Select(_ => _.ShortWebsiteUrl).ToList();
            var pending = new List<string>();
            foreach (var info in names) {
                if (!allN.Contains(info)) {
                    pending.Add(info);
                }
            }

            Log.Logger.Information($"该版本[assets]文件夹下含有 {names.Count} 个mod，{pending.Count} 个mod需要单独处理");

            var dict = await JsonReader.ReadIntroAsync(cfg.Version);

            if (names.Count > cfg.Count) {
                var bin = allM.Where(_ => !names.Contains(_.ShortWebsiteUrl));
                var l = allM.ToList();
                foreach (var info in bin) {
                    l.Remove(info);
                }

                allM = l.ToArray();
            }
            parser.Infos = allM.ToList();
            var l1 = parser.SerializeAll();

            var semaphore = new Semaphore(32, 40);
            foreach (var l in l1) {
                try {
                    semaphore.WaitOne();
                    await Utils.ParseMods(l);
                }
                catch (Exception e) {
                    Log.Logger.Error(e.Message);
                }
                finally {
                    semaphore.Release();
                }
            }

            foreach (var name in pending) {
                if (dict.ContainsKey(name)) {
                    var m = await UrlLib.GetModInfoAsync(dict[name]);
                    var i = parser.Serialize(m);
                    try {
                        await Utils.ParseMods(i);
                    }
                    catch (Exception e) {
                        Log.Logger.Error(e.Message);
                    }
                    Thread.Sleep(5000);
                }
            }
        }
    }
}