﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{

    public class CollectStaticWebAssetsToCopy : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Output]
        public ITaskItem[] AssetsToCopy { get; set; }

        public override bool Execute()
        {
            var copyToOutputFolder = new List<ITaskItem>();
            var normalizedOutputPath = StaticWebAsset.NormalizeContentRootPath(Path.GetFullPath(OutputPath));
            try
            {
                foreach (var asset in Assets.Select(a => StaticWebAsset.FromTaskItem(a)))
                {
                    string fileOutputPath = null;                    
                    if (!(asset.IsDiscovered() || asset.IsComputed()))
                    {
                        Log.LogMessage("Skipping asset '{0}' since source type is '{1}'", asset.Identity, asset.SourceType);
                        continue;
                    }

                    if (asset.IsForReferencedProjectsOnly())
                    {
                        Log.LogMessage("Skipping asset '{0}' since asset mode is '{1}'", asset.Identity, asset.AssetMode);
                    }

                    if (asset.ShouldCopyToOutputDirectory())
                    {
                        // We have an asset we want to copy to the output folder.
                        fileOutputPath = Path.Combine(normalizedOutputPath, asset.RelativePath);
                        string source = null;
                        if (asset.IsComputed())
                        {
                            if (File.Exists(asset.Identity))
                            {
                                Log.LogMessage("Source for asset '{0}' is '{0}' since the asset exists.", asset.Identity, asset.OriginalItemSpec);
                                source = asset.Identity;
                            }
                            else
                            {
                                Log.LogMessage("Source for asset '{0}' is '{1}' since the asset does not exist.", asset.Identity, asset.OriginalItemSpec);
                                source = asset.OriginalItemSpec;
                            }
                        }
                        else
                        {
                            source = asset.Identity;
                        }

                        copyToOutputFolder.Add(new TaskItem(source, new Dictionary<string, string>
                        {
                            ["OriginalItemSpec"] = asset.Identity,
                            ["TargetPath"] = fileOutputPath,
                            ["CopyToOutputDirectory"] = asset.CopyToOutputDirectory
                        }));
                    }
                    else
                    {
                        Log.LogMessage("Skipping asset '{0}' since copy to output directory option is '{1}'", asset.Identity, asset.CopyToOutputDirectory);
                    }
                }

                AssetsToCopy = copyToOutputFolder.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}