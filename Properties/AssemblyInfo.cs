#region using

using System.Reflection;
using log4net.Config;
using System.Runtime.InteropServices;

#endregion

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("This system service controls the SpamAssassin Daemon (spamd.exe)")]
[assembly: AssemblyDescription("SpamAssassin Daemon Control (spamd.exe) Service")]
[assembly: AssemblyCompany("RvdH (vdhout.nl), Apache Software Foundation")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
 [assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyProduct("SpamAssassinService")]
[assembly: AssemblyCopyright("Copyright ©  2021")]
[assembly: XmlConfigurator(Watch = true)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("cc9b2ff8-a11d-47b2-8c44-360befc902e9")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("4.5.1.4")]
[assembly: AssemblyFileVersion("4.5.1.4")]