﻿using System;
using System.IO;

namespace MatterHackers.Agg.UI
{
    public abstract class FileDialogCreator
    {
		public delegate void OpenFileDialogDelegate( OpenFileDialogParams openParams );
		public delegate void SelectFolderDialogDelegate( SelectFolderDialogParams folderParams );
		public delegate void SaveFileDialogDelegate( SaveFileDialogParams saveParams );

		public abstract bool OpenFileDialog(OpenFileDialogParams openParams, OpenFileDialogDelegate callback);
		public abstract bool SelectFolderDialog(SelectFolderDialogParams folderParams, SelectFolderDialogDelegate callback);
		public abstract bool SaveFileDialog(SaveFileDialogParams saveParams, SaveFileDialogDelegate callback);
    }

    public static class FileDialog
    {
        static string lastDirectoryUsed = "";

        static FileDialogCreator fileDialogCreatorPlugin = null;
        static FileDialogCreator FileDialogCreatorPlugin
        {
            get
            {
                if (fileDialogCreatorPlugin == null)
                {
                    string pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    PluginFinder<FileDialogCreator> fileDialogCreatorPlugins = new PluginFinder<FileDialogCreator>(pluginPath);
                    if (fileDialogCreatorPlugins.Plugins.Count != 1)
                    {
                        throw new Exception(string.Format("Did not find any FileDialogCreators in Plugin path ({0}.", pluginPath));
                    }

                    fileDialogCreatorPlugin = fileDialogCreatorPlugins.Plugins[0];
                }

                return fileDialogCreatorPlugin;
            }
        }
        
		public static bool OpenFileDialog(OpenFileDialogParams openParams, FileDialogCreator.OpenFileDialogDelegate callback)
		{
            return FileDialogCreatorPlugin.OpenFileDialog(openParams, (OpenFileDialogParams outputOpenParams) =>
                {
                    try
                    {
                        if (outputOpenParams.FileName != "")
                        {
                            lastDirectoryUsed = Path.GetDirectoryName(outputOpenParams.FileName);
                        }
                    }
                    catch(Exception)
                    {
                    }
                    callback(outputOpenParams);
                }
            );
		}

		public static bool SelectFolderDialog(SelectFolderDialogParams folderParams, FileDialogCreator.SelectFolderDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.SelectFolderDialog(folderParams, callback);
		}

		public static bool SaveFileDialog(SaveFileDialogParams saveParams, FileDialogCreator.SaveFileDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.SaveFileDialog(saveParams, callback);
		}

        public static string LastDirectoryUsed
        {
            get
            {
                if (lastDirectoryUsed == null
                    || lastDirectoryUsed == ""
                    || !Directory.Exists(lastDirectoryUsed))
                {
                    return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                }

                return lastDirectoryUsed;
            }

            set
            {
                lastDirectoryUsed = value;
            }
        }
    }
}
