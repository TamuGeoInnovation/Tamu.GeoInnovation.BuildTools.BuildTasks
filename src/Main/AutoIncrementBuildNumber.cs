using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;
using System.IO;
using Microsoft.VisualStudio.SourceSafe.Interop;

namespace USC.GISResearchLab.Common.BuildTasks
{

    // Code from http://www.csharpfans.com/post/2007/10/Auto-Increment-Revision-Number-in-AssemblyInfocs.aspx
    // and http://www.codeproject.com/KB/dotnet/Auto_Increment_Version.aspx#xx1406378xx
    public class AutoIncrementBuildNumber : Task
    {

        public static string VSSPath = @"\\192.168.2.222\CodeRepository\";
        public static string BaseVSSPath = "$/DotNetDevelopment/";

        #region Properties
        private string _AssemblyFileLocation;
        private string _ExecuteFileLocation;
        private VSSDatabase _VSSDatabase;
        private VSSItem _VSSItem;
        
	
	
        [Required()]
        public string AssemblyFileLocation
        {
            get
            {
                return _AssemblyFileLocation;
            }
            set
            {
                _AssemblyFileLocation = value;
            }
        }

        [Required()]
        public string ExecuteFileLocation
        {
            get
            {
                return _ExecuteFileLocation;
            }
            set
            {
                _ExecuteFileLocation = value;
            }
        }

        public string VSSFileLocation
        {
            get
            {
                string ret = null;
                if (File.Exists(AssemblyFileLocation))
                {
                    string fileName = new FileInfo(ExecuteFileLocation).Name;
                    string ext = new FileInfo(ExecuteFileLocation).Extension;
                    string name = fileName.Substring(0, fileName.IndexOf(ext));
                    ret = BaseVSSPath + name.Replace('.', '/') + "/src/Properties/AssemblyInfo.cs";
                }
                return ret;
            }
        }

        public VSSDatabase VSSDatabase
        {
            get { return _VSSDatabase; }
            set { _VSSDatabase = value; }
        }

        public VSSItem VSSItem
        {
            get { return _VSSItem; }
            set { _VSSItem = value; }
        }

        #endregion

        public override bool Execute()
        {
            try
            {
                return IncrementVersion();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
                return false;
            }
        }

        public bool IncrementVersion()
        {

            int i;
            string[] fileData;
            string s;
            string version;
            string[] v;
            bool resetRevision = false;

            bool checkedOut = false;

            if (File.Exists(AssemblyFileLocation))
            {

                checkedOut = CheckOutAssembly();

                if (!checkedOut)
                {
                    FileAttributes attr = File.GetAttributes(AssemblyFileLocation);
                    if ((attr & FileAttributes.ReadOnly) != 0)
                    {
                        attr -= FileAttributes.ReadOnly;
                        File.SetAttributes(AssemblyFileLocation, attr);
                    }
                }


                TimeSpan diff = new TimeSpan(DateTime.Now.Ticks - DateTime.Parse("JAN/01/2000").Ticks);
                int major = 1;
                int minor = 0;
                int buildNumber = diff.Days;
                int oldBuildNumber = 1;
                int revision = 0;

                //Check the existing build number
                //If the number is not equals, reset the revision number
                if (File.Exists(ExecuteFileLocation))
                {
                    using (FileStream fs = new FileStream(ExecuteFileLocation, FileMode.Open))
                    {
                        byte[] readByte = new byte[fs.Length];
                        fs.Read(readByte, 0, readByte.Length);
                        Assembly assembly = Assembly.Load(readByte);
                        oldBuildNumber = assembly.GetName().Version.Build;

                        if (oldBuildNumber != buildNumber)
                        {
                            resetRevision = true;
                        }
                    }
                }

                try
                {
                    fileData = File.ReadAllLines(AssemblyFileLocation);

                    if (fileData.Length == 0)
                    {
                        return false;
                    }

                    for (i = 0; i < fileData.Length; i++)
                    {
                        s = fileData[i];
                        if (s.Length > 2)
                        {
                            //Look to see if it contains one of the 2 version lines we want.
                            //VB: <Assembly: AssemblyVersion("0.0.0.0")> 
                            //VB: <Assembly: AssemblyFileVersion("0.0.0.0")> 
                            //C#: [assembly: AssemblyFileVersion("1.0.0.0")]
                            //C#: [assembly: AssemblyVersion("1.0.0.0")]
                            if (!(s.Substring(0, 1) == "'") && !(s.Substring(0, 2) == "//"))
                            {

                                if ((s.Contains("AssemblyVersion")) || (s.Contains("AssemblyFileVersion")))
                                {
                                    version = s.Substring(s.IndexOf(Convert.ToChar(34)) + 1);
                                    version = version.Substring(0, version.IndexOf(Convert.ToChar(34)));

                                    v = version.Split(new char[] { '.' });
                                    if (v.Length >= 0)
                                        major = Convert.ToInt32(v[0]);
                                    if (v.Length >= 1)
                                        minor = Convert.ToInt32(v[1]);
                                    if (v.Length >= 2)
                                        resetRevision = (buildNumber != Convert.ToInt32(v[2]));
                                    if (v.Length >= 3)
                                        revision = Convert.ToInt32(v[3]) + 1;

                                    if (resetRevision)
                                    {
                                        revision = 1;
                                    }

                                    fileData[i] = fileData[i].Replace(version, major + "." + minor + "." + buildNumber + "." + revision);
                                }
                            }
                        }
                    }

                    File.WriteAllLines(AssemblyFileLocation, fileData);

                    if (checkedOut)
                    {
                        CheckInAssembly();
                    }

                }
                catch (Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show("ERROR! " + ex.Message + "\n" + ex.StackTrace, "Build Tasks");
                    Log.LogError(ex.Message);
                    return false;
                }
            }

            //return success
            return true;
        }

        ///
        /// Checkout the assembly file from the sourcesafe database
        ///
        public bool CheckOutAssembly()
        {
            bool ret = false;
            if (VSSFileLocation != null && VSSFileLocation.Length > 0)
            {


                try
                {
                    VSSDatabase = new VSSDatabase();
                    VSSDatabase.Open(Path.Combine(VSSPath, "srcsafe.ini"), Environment.UserName, string.Empty);
                    VSSItem = VSSDatabase.get_VSSItem(VSSFileLocation, false);
                    int flags = (int)(VSSFlags.VSSFLAG_BINTEXT | VSSFlags.VSSFLAG_CHKEXCLUSIVENO | VSSFlags.VSSFLAG_CMPFAIL | VSSFlags.VSSFLAG_GETYES);
                    VSSItem.Checkout("Check out by build to increment version", AssemblyFileLocation, flags);
                    ret = true;
                }
                catch (Exception)
                {
                    if (BuildEngine != null)
                    {
                        //Log.LogMessage(MessageImportance.High, ex.Message, null);
                        //MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
                    }
                }
            }
            return ret;
        }

        ///
        /// Checkin the assembly file from the sourcesafe database
        ///
        public bool CheckInAssembly()
        {
            bool ret = false;
            if (VSSFileLocation != null && VSSFileLocation.Length > 0)
            {
                try
                {
                    VSSItem.Checkin("Check in by build to increment version", AssemblyFileLocation, 0);
                    VSSDatabase.Close();
                    ret = true;
                }
                catch (Exception)
                {
                    if (BuildEngine != null)
                    {
                        //Log.LogMessage( MessageImportance.High,  ex.Message, null);
                        //MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
                    }
                }
            }
            return ret;
        }
    }
}
