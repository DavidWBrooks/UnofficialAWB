// FixResx
//  Copyright © 2025 David Brooks
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//
// DESCRIPTION
//
// Updates a localized resource file to reflect the geometries present in the
// Designer.cs file from which it was originally created. This is to work around
// an apparent bug in the way Visual Studio (or maybe the runtime) re-calculates
// the number of pixels on a screen whose DPI is different from the development machine.
//
// Start by making a copy (<working>) of the unlocalized source in <original>.
// In <working>, use Visual Studio Designer on a Form to make it Localizable Yes.
// Run this app. It will edit the resx file in <working> to reflect the actual
// measurements of the same properties in <original>.
//
// Assume nobody has edited the standard layout of each, or the way the integer values
// are presented, or interesting errors will happen.
//
// Edits made:
// Size, SizeF, Point: use original x, y
// Margin: use original Padding values (1 or 4 integer). If none specified, remove the resx entry
//
// TO BE DETERMINED - check the autosize stuff, or did that stay in the cs file?
// TO DO - resourceize specification of working directories and logfile
// TO DO - more error handling?
// TO DO - decide whether to use the working dir or the original dirs as template (ask the user)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace FixResx
{
    internal class Program
    {
        // Updates made in place, whether or not it's already localized. Should be close to HEAD.
        const string workingDir = @"D:\ResxFixes";
        // Template of the original localization attempts, to edit and copy to working. SVN 12745.
        const string postLocDir = @"D:\PreReverts";
        // Pre-localization dir, with the canonical measurements. SVN 12731.
        const string preLocDir = @"D:\PreLocalization";
        readonly static string logfile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FixResx.log");

        // These do not need to be detailed about the value contents; they should look the same
        readonly static Regex FindInDesignerRE =
            new Regex(@"^\s*([^\s]+) = new System.Drawing.(?:SizeF?|Point|Location|Padding)\((.+)\);");
        readonly static Regex FindSplitterInDesignerRE =
            new Regex(@"^\s*([^\s]+Splitter(?:Distance|Width)) = (\d+)");
        readonly static Regex FindInResxRE =
            new Regex("<data name=\"\\$?(.+)\" type=\"System.(?:Drawing.(?:SizeF?|Point|Location)|Windows.Forms.Padding)");
        readonly static Regex FindSplitterInResxRE =
            new Regex("<data name=\"\\$?(.+\\.Splitter(?:Distance|Width))\"");

        static void Main(string[] args)
        {
            // Argument: a single file name including relative dir, without extensions
            string basename;

            if (args.Length == 0) {
                Console.Write("Enter a single root pathname: ");
                basename = Console.ReadLine();
            } else {
                basename = args[0];
            }

            string designerBase = Path.Combine(preLocDir, basename + ".Designer.cs");
            string designerTemplate = Path.Combine(postLocDir, basename + ".Designer.cs");
            string resxTemplate = Path.Combine(postLocDir, basename + ".resx");
            string newDesigner = Path.Combine(workingDir, basename + ".Designer.cs");
            string newResx = Path.Combine(workingDir, basename + ".resx");
            string tempResx = newResx + ".new";

            if (!File.Exists(newDesigner)) {
                Console.WriteLine($"\"{basename}\" is invalid.");
                return;
            }

            Dictionary<string, string> cslist = new Dictionary<string, string>();
            Dictionary<string, string> resxlist = new Dictionary<string, string>();

            List<string> log = new List<string>
            {
                $"FixResx - {basename} - {DateTime.Now}",
                null
            };

            // Index original properties (with an explicit "this." at top level) and save the parameters within parentheses
            // A typical line is:
            //   this.okButton.Size = new System.Drawing.Size(75, 23);
            foreach (string line in File.ReadLines(designerBase)) {
                Match settingm = FindInDesignerRE.Match(line);
                if (!settingm.Success) {
                    settingm = FindSplitterInDesignerRE.Match(line);

                }
                if (settingm.Success) {
                    string name = settingm.Groups[1].Value;
                    // Keep specific "this" only for its own settings
                    if (name.StartsWith("this.")) { // which I think it always does
                        string afterthis = name.Substring(5);
                        if (afterthis.Contains("."))
                            name = afterthis;
                    }
                    cslist[name] = settingm.Groups[2].Value.Replace("F", "");
                }
            }

            // Start to analyze the resx - first, replace the canned help text
            using (StreamWriter sw = new StreamWriter(tempResx, false, Encoding.UTF8)) {
                using (Stream gpl = Assembly.GetExecutingAssembly().GetManifestResourceStream("FixResx.GPL.txt")) {
                    gpl.CopyTo(sw.BaseStream);
                }
                using (StreamReader sr = new StreamReader(resxTemplate)) {
                    while (!sr.EndOfStream && !sr.ReadLine().Contains("-->")) { }

                    // Go looking for a trio of lines like this:
                    // <data name="okButton.Size" type="System.Drawing.Size, System.Drawing">
                    //   <value>75, 23</ value >
                    // </data>
                    // Note the value matches the contents of the brackets in the Designer code.
                    // Also ignore leading $ before "this"
                    while (!sr.EndOfStream) {
                        string nextline = sr.ReadLine();
                        Match settingm = FindInResxRE.Match(nextline);
                        if (!settingm.Success) {
                            settingm = FindSplitterInResxRE.Match(nextline);
                        }
                        if (settingm.Success) {
                            string name = settingm.Groups[1].Value;
                            string valueline = sr.ReadLine();
                            string enddataline = sr.ReadLine();
                            bool skipdata = false;
                            Match valuem = Regex.Match(valueline, @"^\s*<value>(.+)</value>\s*$");
                            if (valuem.Success) {
                                string oldval = valuem.Groups[1].Value;
                                resxlist[name] = oldval;
                                // Is it in the original Designer file?
                                if (cslist.TryGetValue(name, out string orignums)) {
                                    valueline = valueline.Replace(oldval, orignums);
                                    log.Add($"{name} => {orignums}");
                                } else {
                                    // We get here when the value is just digits but it is not represented in
                                    // the original cs file. The cases I know are Margin and Padding (in menus)
                                    // and SplitterWidth, where the defaults were re-scaled.
                                    if (name.EndsWith("Margin") || name.EndsWith("Padding") || name.EndsWith("SplitterWidth")) {
                                        skipdata = true;
                                        resxlist.Remove(name);
                                        log.Add($"{name} skipped");
                                    } else {
#if DEBUG
                                        throw new ApplicationException("Unexpected new property: " + name);
#endif
                                    }
                                }
                            }
                            if (!skipdata) {
                                sw.WriteLine(nextline);
                                sw.WriteLine(valueline);
                                sw.WriteLine(enddataline);
                            }
                        } else {
                            sw.WriteLine(nextline);
                        }
                    }
                }
            }

            // Replace the files in the active tree
            try {
                File.Delete(newResx);
                File.Move(tempResx, newResx);
                File.Copy(designerTemplate, newDesigner, true);
                Console.WriteLine("New files written");
            }
            catch (Exception e) {
                string message = "Exception when replacing files: " + e.Message;
                Console.WriteLine(message);
                Console.WriteLine("You may want to check for a partial update");
                log.Add("*** " + message + " ***");
            }

            // Report on unmatched keys on both sides
            foreach (string name in cslist.Keys.Except(resxlist.Keys)) {
                log.Add($"*** {name} in Designer.cs but not resx");
            }
            foreach (string name in resxlist.Keys.Except(cslist.Keys)) {
                log.Add($"*** {name} in resx but not Designer.cs");
            }

            log.Add(String.Empty);
            File.AppendAllLines(logfile, log, Encoding.UTF8);
        }
    }
}
