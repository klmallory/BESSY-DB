using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Resources;
using System.Security;
using System;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("BESSy")]
[assembly: AssemblyDescription("Binary Embedded Storage System.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("KlInk")]
[assembly: AssemblyProduct("BESSy")]
[assembly: AssemblyCopyright("Copyright © KlInk 2012-2014")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AllowPartiallyTrustedCallers]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a tBuilder in this assembly from 
// COM, set the ComVisible attribute to true on that tBuilder.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("e5d1161d-1036-49a2-8f85-906d7465b51c")]

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
//makecert -pe -r -len 2048 -a sha512 -sv BESSy.Tests.pvk BESSy.Tests.cer
//pvk2pfx -pvk BESSy.Tests.pvk -pi ##### -spc BESSy.Tests.cer -pfx BESSy.Tests.keyfile.pfx -f
//sn -p BESSy.Tests.keyfile.pfx BESSy.Tests.keyfile.pub
//sn -tp BESSy.Tests.keyfile.pub

[assembly: AssemblyVersion("0.9.1.1")]
[assembly: AssemblyFileVersion("0.9.1.1")]
[assembly: CLSCompliant(true)]
[assembly: NeutralResourcesLanguageAttribute("en-US")]


#if NCRUNCH || DEBUG
[assembly: InternalsVisibleTo(@"BESSy.Tests")]
[assembly: InternalsVisibleTo(@"BESSy.TestAssembly")]
#else
[assembly: InternalsVisibleTo(@"BESSy.Tests, PublicKey=0024000004800000140100000602000000240000525341310008000001000100ff878adb31c8ba45c1febfd16a0df1b7de06de6e8d9aed5e48c4051349e4f5b1d36788b767722bf0cc41c6e8fc3b6c8fd43f017d2a4a6bc090598da6e24c5ea6a2e867672f25a78bacbd15b99efa3e1adb7277b1338667cfb779263dffe4e00bab79778ee2dd6ec271e1f6e47640f2772ad34114eaf463e0034eb6b6d9f73ff5435986e5f724174fd5623637388f92977e69e1318efb9143e3c57b8467d07eabbe98f26aa681db80801c70898733371763ea50d2a4c33df59f1aad7db84303e8e74dc7589c5600fcd3095d898b6cf81933b80e08c772162fa438002ab7cbfbd862851759a85042cfe7de16d93afc55babebc0873e90ecad9f15d6568e266329c")]
[assembly: InternalsVisibleTo(@"BESSy.TestAssembly, PublicKey=0024000004800000140100000602000000240000525341310008000001000100ff878adb31c8ba45c1febfd16a0df1b7de06de6e8d9aed5e48c4051349e4f5b1d36788b767722bf0cc41c6e8fc3b6c8fd43f017d2a4a6bc090598da6e24c5ea6a2e867672f25a78bacbd15b99efa3e1adb7277b1338667cfb779263dffe4e00bab79778ee2dd6ec271e1f6e47640f2772ad34114eaf463e0034eb6b6d9f73ff5435986e5f724174fd5623637388f92977e69e1318efb9143e3c57b8467d07eabbe98f26aa681db80801c70898733371763ea50d2a4c33df59f1aad7db84303e8e74dc7589c5600fcd3095d898b6cf81933b80e08c772162fa438002ab7cbfbd862851759a85042cfe7de16d93afc55babebc0873e90ecad9f15d6568e266329c")]
#endif

//[assembly: InternalsVisibleTo("BESSy.Tests, PublicKey=0024000004800000140100000602000000240000525341310008000001000100ff878adb31c8ba45c1febfd16a0df1b7de06de6e8d9aed5e48c4051349e4f5b1d36788b767722bf0cc41c6e8fc3b6c8fd43f017d2a4a6bc090598da6e24c5ea6a2e867672f25a78bacbd15b99efa3e1adb7277b1338667cfb779263dffe4e00bab79778ee2dd6ec271e1f6e47640f2772ad34114eaf463e0034eb6b6d9f73ff5435986e5f724174fd5623637388f92977e69e1318efb9143e3c57b8467d07eabbe98f26aa681db80801c70898733371763ea50d2a4c33df59f1aad7db84303e8e74dc7589c5600fcd3095d898b6cf81933b80e08c772162fa438002ab7cbfbd862851759a85042cfe7de16d93afc55babebc0873e90ecad9f15d6568e266329c")]
