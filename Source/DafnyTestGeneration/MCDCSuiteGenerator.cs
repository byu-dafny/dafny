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
        //private Dictionary<Expr, String> exprToString = new Dictionary<Expr, string>();
        private Dictionary<String, Expr> symbolToExpr = new ();
        private int decisionNum = 0;


        public Dictionary<GotoCmd, List<Dictionary<String, bool>>> GetTestSuite(Dictionary<GotoCmd, Decision> decisions) {
            Dictionary<GotoCmd, List<Dictionary<String, bool>>> allTestSets = new ();
            foreach (var entry in decisions) {
                //var modifiedDecision = Preprocessing(entry.Value.ToString());
                var modifiedDecision = Preprocessing(entry.Value);
                Console.Out.Write("// decision modified to " + modifiedDecision + "\n");

                var testSet = PyMCDCFacade.getTestSet(modifiedDecision);

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
                var symbolName = varNaryPrefix + i.ToString() + 'd' + decisionNum.ToString();
                //Console.Out.Write("current nary is " + decision.exprNarySet[i].ToString() + "\n");
                strToModify = strToModify.Replace(decision.exprNarySet[i].ToString().Trim(), symbolName);
                //Console.Out.Write("modified to " + strToModify + "\n");
                symbolToExpr.Add(symbolName, decision.exprNarySet[i]);
            }
            for (var i = 0; i < decision.exprIdentSet.Count; i++) {
                var symbolName = varIdentPrefix + i.ToString() + 'd' + decisionNum.ToString();
                strToModify = strToModify.Replace(decision.exprIdentSet[i].ToString().Trim(), symbolName);
                symbolToExpr.Add(symbolName, decision.exprIdentSet[i]);
            }
            decisionNum++;
            
            strToModify = strToModify.Replace("&&", "&");
            strToModify = strToModify.Replace("||", "|");
            return strToModify;
        }

        // private String Preprocessing(String decision) {
        //     var exprClone = new String(decision);

        //     exprClone = Regex.Replace(exprClone, "[()]", String.Empty);
        //     var exprList = new List<String>(exprClone.Split(new [] {"&&", "||"}, StringSplitOptions.RemoveEmptyEntries));
        //     Console.Out.Write(exprList[0]);

        //     for (var i = 0; i < exprList.Count; i++) {
        //         exprList[i] = exprList[i].Trim();

        //         var symbolName = varPrefix + i.ToString();
        //         decision = decision.Replace(exprList[i], symbolName);
        //         symbolStrToExpr.Add(symbolName, exprList[i]);
        //     }

        //     decision = decision.Replace("&&", "&");
        //     decision = decision.Replace("||", "|");
        //     return decision;
        // }

        class PyMCDCFacade {
            public static List<Dictionary<String, bool>>? getTestSet(String decision) {
                
                string fileName = @"/home/deant4/dafny/Source/DafnyTestGeneration/mcdc_test/main.py " + "\"" + decision + "\"";

                Process p = new Process();
                p.StartInfo = new ProcessStartInfo(@"python3", fileName)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                p.Start();

                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                var testSet = JsonConvert.DeserializeObject<List<Dictionary<String, bool>>>(output); 
                Console.Out.Write("//" + output.ToString());

                return testSet;
            }
        }
    }
}