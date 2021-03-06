﻿//Copyright (c) 2016-2019 nvisionative, Inc.

//This file is part of nvQuickSite.

//nvQuickSite is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//nvQuickSite is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with nvQuickSite.  If not, see <http://www.gnu.org/licenses/>.

using nvQuickSite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Windows.Forms;
using Octokit;

namespace nvQuickSite.Controllers
{
    public class PackageController
    {
        public static IEnumerable<Package> GetPackageList()
        {
            var localPackages = GetLocalPackages();
            var packages = localPackages.ToList();
            var remotePackages = GetRemotePackages();
            if (remotePackages.Count() > 0)
            {
                packages = localPackages.Where(p => p.keep == true).ToList();
                foreach (var package in remotePackages)
                {
                    if (packages.SingleOrDefault(p => p.did == package.did && p.version == package.version) == null)
                    {
                        packages.Add(package);
                    }
                }
            }
            var ghPackages = GetGitHubPackages();
            if (ghPackages.Count() > 0)
            {
                packages = packages.Union(ghPackages).ToList();
            }
            SaveLocalPackagesFile(packages);
            return packages;
        }

        private static IEnumerable<Package> GetLocalPackages()
        {
            var res = new List<Package>();
            var pfile = Directory.GetCurrentDirectory() + @"\Downloads\packages.json";
            if (File.Exists(pfile))
            {
                using (var sr = new StreamReader(pfile))
                {
                    var content = sr.ReadToEnd();
                    res = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Package>>(content);
                }
            }
            return res;
        }

        private static void SaveLocalPackagesFile(IEnumerable<Package> packages)
        {
            var downloadDirectory = GetDownloadDirectory();
            if (!Directory.Exists(downloadDirectory))
            {
                Directory.CreateDirectory(downloadDirectory);
            }

            var pfile = Directory.GetCurrentDirectory() + @"\Downloads\packages.json";
            using (var sw = new StreamWriter(pfile))
            {
                sw.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(packages, Newtonsoft.Json.Formatting.Indented));
            }
        }

        private static IEnumerable<Package> GetRemotePackages()
        {
            WebClient client = new WebClient();
            try
            {
                var url = "https://github.com/nvisionative/nvQuickSite/raw/master/nvQuickSite/data/packages.json";
                string result = client.DownloadString(url);
                var res = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<Package>>(result);
                return res;
            }
            catch (Exception ex)
            {
            }
            return new List<Package>();
        }

        private static string GetDownloadDirectory()
        {
            return Directory.GetCurrentDirectory() + @"\Downloads\";
        }

        private static IEnumerable<Package> GetGitHubPackages()
        {
            var res = new List<Package>();
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("nvQuickSite"));
                var releases = client.Repository.Release.GetAll("dnnsoftware", "Dnn.Platform").Result;

                if (releases.Count > 0)
                {
                    var index = 0;
                    foreach (Release release in releases)
                    {
                        var installPackage = release.Assets.Where(a => a.BrowserDownloadUrl.IndexOf("install", StringComparison.OrdinalIgnoreCase) > -1 && a.BrowserDownloadUrl.IndexOf("dnn_platform", StringComparison.OrdinalIgnoreCase) > -1).FirstOrDefault();
                        var upgradePackage = release.Assets.Where(a => a.BrowserDownloadUrl.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) > -1 && a.BrowserDownloadUrl.IndexOf("dnn_platform", StringComparison.OrdinalIgnoreCase) > -1).FirstOrDefault();
                        var ghPackage = new Package();

                        ghPackage.version = TrimTagName(release);

                        if (index == 0 && 
                            release.Name.IndexOf("rc", StringComparison.OrdinalIgnoreCase) >= 0 && 
                            Properties.Settings.Default.ShowReleaseCandidates && 
                            installPackage != null)
                        {
                            ghPackage.did = "dnn-platform-rc";
                            ghPackage.name = "DNN Platform Release Candidate";
                            ghPackage.url = installPackage.BrowserDownloadUrl;
                            ghPackage.upgradeurl = upgradePackage.BrowserDownloadUrl;
                            res.Add(ghPackage);
                        }
                        else if (!release.Name.ToLower().Contains("rc") &&
                            installPackage != null)
                        {
                            ghPackage.did = "dnn-platform-" + ghPackage.version.Substring(0, 1);
                            ghPackage.name = "DNN Platform " + ghPackage.version.Substring(0, 1);
                            ghPackage.url = installPackage.BrowserDownloadUrl;
                            ghPackage.upgradeurl = upgradePackage.BrowserDownloadUrl;
                            res.Add(ghPackage);
                        }
                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return res;
        }

        private static string TrimTagName(Release release)
        {
            if (release.TagName != null && release.TagName[0] == 'v')
                return release.TagName.Remove(0, 1);
            else
                return release.TagName;
        }

    }
}