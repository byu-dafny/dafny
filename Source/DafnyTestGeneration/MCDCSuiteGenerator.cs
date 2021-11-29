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

        private const Char varPrefix = 'v';
        //private Dictionary<Expr, String> exprToString = new Dictionary<Expr, string>();
        private Dictionary<String, String> symbolStrToExpr = new Dictionary<String, String>();

        public Dictionary<String, String> SymbolStrToExpr { get {return symbolStrToExpr;} }

        public Dictionary<Expr, List<Dictionary<String, bool>>?>? GetTestSuite(Dictionary<GotoCmd, Expr> decisions) {
            Dictionary<Expr, List<Dictionary<String, bool>>?>? allTestSets = new Dictionary<Expr, List<Dictionary<string, bool>>?>();
            foreach (var entry in decisions) {
                var modifiedDecision = Preprocessing(entry.Value.ToString());
                Console.Out.Write("decision modified to " + modifiedDecision + "\n");

                var testSet = PyMCDCFacade.getTestSet(modifiedDecision);
                allTestSets.Add(entry.Value, testSet);
            }
            return allTestSets;
        }

        private String Preprocessing(String decision) {
            var exprClone = new String(decision);

            exprClone = Regex.Replace(exprClone, "[()]", String.Empty);
            var exprList = new List<String>(exprClone.Split(new [] {"&&", "||"}, StringSplitOptions.RemoveEmptyEntries));
            Console.Out.Write(exprList[0]);

            for (var i = 0; i < exprList.Count; i++) {
                exprList[i] = exprList[i].Trim();

                var symbolName = varPrefix + i.ToString();
                decision = decision.Replace(exprList[i], symbolName);
                symbolStrToExpr.Add(symbolName, exprList[i]);
            }

            decision = decision.Replace("&&", "&");
            decision = decision.Replace("||", "|");
            return decision;
        }

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
                Console.Out.Write(output.ToString());

                return testSet;
            }
        }
    }
}