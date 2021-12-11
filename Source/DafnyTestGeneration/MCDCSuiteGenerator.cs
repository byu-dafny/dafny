using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;
using Microsoft.Dafny;
using System.Reflection;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Newtonsoft.Json;

namespace DafnyTestGeneration {
    public class MCDCSuiteGenerator {

        private const Char varIdentPrefix = 'i';
        private const Char varNaryPrefix = 'n';
        private const Char decisionPrefix = 'd';
        private int decisionNum = 0;
        private Dictionary<String, Expr> symbolToExpr = new ();

        public Dictionary<GotoCmd, List<Dictionary<String, bool>>> GetTestSuite(Dictionary<GotoCmd, Decision> decisions) {
            Dictionary<GotoCmd, List<Dictionary<String, bool>>> allTestSets = new();
            foreach (var entry in decisions) {
                var modifiedDecision = Preprocessing(entry.Value);
                //Console.Out.WriteLine("// decision modified to " + modifiedDecision);

                var testSet = Generator.getTestSet(modifiedDecision);

                if (testSet != null) {
                    Postprocessing(testSet);

                    allTestSets.Add(entry.Key, testSet);
                }
            }
            return allTestSets;
        }

        private void Postprocessing(List<Dictionary<String, bool>> testSet) {
            for (var i = 0; i < testSet.Count; i++) {
                var keys = new List<String>(testSet[i].Keys);
                for (var j = 0; j < keys.Count; j++) {
                    var value = testSet[i][keys[j]];
                    testSet[i].Remove(keys[j]);
                    testSet[i].Add(symbolToExpr[keys[j]].ToString(), value);
                }
            }
        }

        private String Preprocessing(Decision decision) {
            var strToModify = new String(decision.decisionExpr.ToString());

            for (var i = 0; i < decision.exprNarySet.Count; i++) {
                var symbolName = varNaryPrefix + i.ToString() + decisionPrefix + decisionNum.ToString();
                strToModify = strToModify.Replace(decision.exprNarySet[i].ToString().Trim(), symbolName);
                symbolToExpr.Add(symbolName, decision.exprNarySet[i]);
            }
            for (var i = 0; i < decision.exprIdentSet.Count; i++) {
                var symbolName = varIdentPrefix + i.ToString() + decisionPrefix + decisionNum.ToString();
                strToModify = strToModify.Replace(decision.exprIdentSet[i].ToString().Trim(), symbolName);
                symbolToExpr.Add(symbolName, decision.exprIdentSet[i]);
            }
            decisionNum++;
            
            strToModify = strToModify.Replace("&&", "&");
            strToModify = strToModify.Replace("||", "|");
            return strToModify;
        }

        class Generator {
            public static List<Dictionary<String, bool>>? getTestSet(String decision) {
                
                var fileName = Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                if (fileName == null)
                    throw new FileNotFoundException("Unable to locate mcdc python script.  Please ensure mcdc_test directory is in DafnyTestGeneration");
                fileName = Path.Combine(fileName, "Source/DafnyTestGeneration/mcdc_test/main.py");
                String fileNameWithArgs = fileName + " \"" + decision + "\"";

                Process p = new Process();
                p.StartInfo = new ProcessStartInfo(@"python3", fileNameWithArgs)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                p.Start();

                String output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                var testSet = JsonConvert.DeserializeObject<List<Dictionary<String, bool>>>(output); 
                //Console.Out.WriteLine("//" + output.ToString());

                return testSet;
            }
        }
    }
}