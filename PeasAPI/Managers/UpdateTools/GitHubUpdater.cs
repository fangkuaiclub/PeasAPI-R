﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using PeasAPI.Enums;

namespace PeasAPI.Managers.UpdateTools
{
    public sealed class GitHubUpdater : UpdateListener
    {
        public GitHubUpdater(Assembly assembly, string owner, string repoName, FileType priority, UpdateType updateType = UpdateType.Every)
        {
            Assembly = assembly;
            JsonLink = $"https://api.github.com/repos/{owner}/{repoName}/releases/latest";
            Priority = priority;
            UpdateType = updateType;
            Initialize();
        }

        private FileType Priority { get; }

        public override void FromJsonElement(JsonElement json)
        {
            var tagVer = json.GetProperty("tag_name").GetString();
            var assetLists = json.GetProperty("assets").EnumerateArray();
            Version = UpdateManager.SanitizeVersion(tagVer);
            AssetLink = GetLinkByPriority(assetLists.ToList());
        }

        public override HttpResponseMessage FetchData()
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PeasAPI Updater");

            return httpClient.GetAsync(new Uri(JsonLink), 0).Result;
        }

        private string GetLinkByPriority(IReadOnlyCollection<JsonElement> array)
        {
            var priority = Priority switch
            {
                FileType.First => null,
                FileType.Dll => "application/x-msdownload",
                FileType.Zip => "application/x-zip-compressed",
                _ => null
            };

            if (!array.Any()) return null;

            var first = array.Cast<JsonElement?>().FirstOrDefault(x =>
                x?.GetProperty("content_type").GetString()
                ?.Equals(priority) ?? true) ?? array.FirstOrDefault();
            
            return first.GetProperty("browser_download_url").GetString();
        }
    }
}