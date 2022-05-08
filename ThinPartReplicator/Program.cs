using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThinPartReplicator
{
    class Program
    {
        const string outputFolder = @"C:\Users\info\source\repos\ThinPartReplicator";
        const string baseFileName = "coffee_mug_holder_open";
        const string layerHeightText = "0.2mm";

        // Feed rate for printing (not sure but I think the units on this are millimeters per minute)
        const double printFeedRate = 1200.000;

        const double layerHeight = 0.2;

        const double totalMinutesBase = 18.667;

        const int nRepsX = 4;
        const int nRepsY = 2;
        const int nReplicants = nRepsX * nRepsY;
        const double xRepStep = 50;
        const double yRepStep = 100;

        static void Main(string[] args)
        {
            double totalMinutes = totalMinutesBase * nReplicants;
            int printMinutes = (int)Math.Round(totalMinutes);
            int printHours = 0;
            while (printMinutes >= 60)
            {
                printHours++;
                printMinutes -= 60;
            }

            string inputHeaderFilename = "coffee_mug_holder_open_header.txt";
            string inputSkirtFilename = "coffee_mug_holder_open_skirt.txt";
            string inputMainFilename = "coffee_mug_holder_open_main.txt";
            string inputFooterFilename = "coffee_mug_holder_open_footer.txt";

            string outputFilename = baseFileName + "_" + nReplicants
#if RECOVER_FAILED_PRINT
                + "_recovering"
#endif // RECOVER_FAILED_PRINT
                + "_replicants_" + layerHeightText + "_PLA_MK3S_" + printHours.ToString() + "h" + printMinutes + "m.gcode";

            string outputPath = Path.Combine(outputFolder, outputFilename);
            using (StreamWriter sw = new StreamWriter(outputPath, false))
            {
                using (StreamReader sr = new StreamReader(Path.Combine(outputFolder, inputHeaderFilename)))
                {
                    string line = sr.ReadLine();
                    while (line != null)
                    {
                        sw.WriteLine(line);
                        line = sr.ReadLine();
                    }
                }

#if !RECOVER_FAILED_PRINT
                // Skirt
                for (int idxY = 0; idxY < nRepsY; idxY++)
                {
                    for (int idxX = 0; idxX < nRepsX; idxX++)
                    {
                        using (StreamReader sr = new StreamReader(Path.Combine(outputFolder, inputSkirtFilename)))
                        {
                            string line = sr.ReadLine();
                            while (line != null)
                            {
                                string modifiedLine = ApplyRep(idxX, idxY, line, false);
                                sw.WriteLine(modifiedLine);
                                line = sr.ReadLine();
                            }
                        }
                    }
                }
#endif // !RECOVER_FAILED_PRINT

                // Main print
                for (int idxY = 0; idxY < nRepsY; idxY++)
                {
                    for (int idxX = 0; idxX < nRepsX; idxX++)
                    {
#if RECOVER_FAILED_PRINT
                        // Debugging to recover a failed print
                        if (idxX == 0 && idxY == 0)
                        {
                            idxX = 1;
                            idxY = 0;
                        }
                        else
                        {
#endif // RECOVER_FAILED_PRINT

                        if (idxX > 0 || idxY > 0)
                        {
                            // Move to filament change position
                            sw.WriteLine("G1 X250 Y0 Z210 ; move to filament change position");

                            // Change filament
                            sw.WriteLine("M600");

                            // Repeat the intro line
                            sw.WriteLine("G28 W ; home all without mesh bed level");
                            sw.WriteLine("G1 Y-3.0 F1000.0 ; go outside print area");
                            sw.WriteLine("G1 E0.80000 F2100.00000; ready filament");
                            sw.WriteLine("G92 E0.0");
                            sw.WriteLine("G1 X60.0 E9.0  F1000.0 ; intro line");
                            sw.WriteLine("G1 X100.0 E12.5  F1000.0 ; intro line");
                            sw.WriteLine("G92 E0.0");
                            sw.WriteLine("G1 E-0.80000 F2100.00000 ; retract filament");
                            sw.WriteLine("G1 Z0.800 F10800.000 ; lift tip up");
                        }
#if RECOVER_FAILED_PRINT
                        }
#endif // RECOVER_FAILED_PRINT

                        using (StreamReader sr = new StreamReader(Path.Combine(outputFolder, inputMainFilename)))
                        {
                            string line = sr.ReadLine();
                            while (line != null)
                            {
                                string modifiedLine = ApplyRep(idxX, idxY, line, true);
                                sw.WriteLine(modifiedLine);
                                line = sr.ReadLine();
                            }
                        }
                    }
                }

                // Retract filament
                sw.WriteLine("G1 E-0.80000 F2100.00000 ; Retract filament");

                using (StreamReader sr = new StreamReader(Path.Combine(outputFolder, inputFooterFilename)))
                {
                    string line = sr.ReadLine();
                    while (line != null)
                    {
                        sw.WriteLine(line);
                        line = sr.ReadLine();
                    }
                }
            }
        }

        private static string ApplyRep(int idxX, int idxY, string originalLine, bool adjustTime)
        {
            double xOffset = idxX * xRepStep;
            double yOffset = idxY * yRepStep;
            double timeOffset = 0.0;
            if (adjustTime)
            {
                timeOffset = totalMinutesBase * (idxY * nRepsX + idxX);
            }

            string comment = "";
            if (originalLine.IndexOf(';') >= 0)
            {
                comment = originalLine.Substring(originalLine.IndexOf(';'));
                originalLine = originalLine.Substring(0, originalLine.IndexOf(';'));
            }
            string[] tokens = originalLine.Split(' ');
            List<string> unprocessedTokens = new List<string>();

            if (tokens.Length > 0)
            {
                if (tokens[0] == "G1")
                {
                    bool hasX = false;
                    bool hasY = false;
                    bool hasZ = false;
                    double x = 0.0;
                    double y = 0.0;
                    double z = 0.0;
                    for (int i = 1; i < tokens.Length; i++)
                    {
                        if (tokens[i].ToUpper().StartsWith("X"))
                        {
                            x = double.Parse(tokens[i].Substring(1));
                            hasX = true;
                        }
                        else if (tokens[i].ToUpper().StartsWith("Y"))
                        {
                            y = double.Parse(tokens[i].Substring(1));
                            hasY = true;
                        }
                        else if (tokens[i].ToUpper().StartsWith("Z"))
                        {
                            z = double.Parse(tokens[i].Substring(1));
                            hasZ = true;
                        }
                        else
                        {
                            unprocessedTokens.Add(tokens[i]);
                        }
                    }
                    double newX = x + xOffset;
                    double newY = y + yOffset;
                    double newZ = z;

                    List<string> newTokens = new List<string>();
                    newTokens.Add(tokens[0]); // G1
                    if (hasX)
                    {
                        newTokens.Add("X" + string.Format("{0,1:F3}", Math.Round(newX, 3)));
                    }
                    if (hasY)
                    {
                        newTokens.Add("Y" + string.Format("{0,1:F3}", Math.Round(newY, 3)));
                    }
                    if (hasZ)
                    {
                        newTokens.Add("Z" + string.Format("{0,1:F3}", Math.Round(newZ, 3)));
                    }
                    newTokens.AddRange(unprocessedTokens);
                    newTokens.Add(comment);
                    return string.Join(" ", newTokens);
                }
                else if (tokens[0] == "M73")
                {
                    // Future todo: we could also use the originally reported minutes remaining, double.Parse(tokens[2].Substring(1)), in some way

                    double minutesElapsed = timeOffset + 0.01 * double.Parse(tokens[1].Substring(1)) * totalMinutesBase;
                    int minutesRemaining = (int)Math.Round(totalMinutesBase * nReplicants - minutesElapsed);
                    int pctDone = (int)Math.Round(100 * minutesElapsed / (totalMinutesBase * nReplicants));

                    return tokens[0] + " " + tokens[1].Substring(0, 1) + pctDone.ToString() + " " + tokens[2].Substring(0, 1) + minutesRemaining.ToString() + " ; updating progress display (" + pctDone.ToString() + "% done, " + minutesRemaining.ToString() + " minutes remaining)";
                }
#if RECOVER_FAILED_PRINT
                else if (tokens[0] == "G80")
                {
                    return ";" + originalLine + comment;  // comment out the mesh bed leveling
                }
#endif // RECOVER_FAILED_PRINT
            }

            return originalLine + comment;
        }
    }
}
