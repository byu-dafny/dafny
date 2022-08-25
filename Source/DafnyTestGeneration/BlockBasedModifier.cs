using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Dafny;
using LiteralExpr = Microsoft.Boogie.LiteralExpr;
using Program = Microsoft.Boogie.Program;
using Token = Microsoft.Boogie.Token;

namespace DafnyTestGeneration {

  /// <summary>
  /// A version of ProgramModifier that inserts assertions into the code
  /// that fail when a particular basic block is visited
  /// </summary>
  public class BlockBasedModifier : ProgramModifier {
    
    private Implementation? implementation; // the implementation currently traversed
    private Program? program; // the original program

    protected override IAsyncEnumerable<ProgramModification> GetModifications(Program p) {
      return VisitProgram(p);
    }
    private ProgramModification? VisitBlock(Block node) {
      var captured = ExtractCapturedStates(node);
      if (captured.Count > 0 && DafnyOptions.O.TestGenOptions.blocksToSkip.Contains(captured.ToList().First())) {
        return null;
      }

      if (program == null || implementation == null) {
        return null;
      }
      base.VisitBlock(node);
      if (node.cmds.Count == 0) { // ignore blocks with zero commands
        return null;
      }

      var procedureName = ImplementationToTarget?.VerboseName ??
                          implementation.VerboseName;
      node.cmds.Add(new AssertCmd(new Token(), new LiteralExpr(new Token(), false)));
      var record = ProgramModification.GetProgramModification(program, implementation, 
        new HashSet<int>() {node.UniqueId}, ExtractCapturedStates(node),
          procedureName, $"{procedureName.Split(" ")[0]}(block#{node.UniqueId})");

      node.cmds.RemoveAt(node.cmds.Count - 1);
      if (record.IsCovered) {
        return null;
      }
      return record;
    }

    private async IAsyncEnumerable<ProgramModification> VisitImplementation(
      Implementation node) {
      implementation = node;
      if (!ImplementationIsToBeTested(node) ||
          !dafnyInfo.IsAccessible(node.VerboseName.Split(" ")[0])) {
        yield break;
      }

      switch (DafnyOptions.O.TestGenOptions.Minimization) {
        case TestGenerationOptions.Minimizations.Random:
          var random = new Random();
          foreach (var block in node.Blocks.OrderBy(_ => random.Next())) {
            var modification = VisitBlock(block);
            if (modification != null) {
              yield return modification;
            }
          }
          break;
        case TestGenerationOptions.Minimizations.Topological:
          // TODO: Verify that this performs topological sort
          for (int i = node.Blocks.Count - 1; i >= 0; i--) {
            var modification = VisitBlock(node.Blocks[i]);
            if (modification != null) {
              yield return modification;
            }
          }
          break;
        case TestGenerationOptions.Minimizations.Optimal:
          var variables = PathBasedModifier.InitBlockVars(node);
          var allPathsFeasible = false;
          int bestResult = 0;
          int minPaths = 1;
          var returnBlocks = node.Blocks
            .Where(block => block.TransferCmd is ReturnCmd).ToList();
          List<ProgramModification> bestResultPaths = new();
          List<ProgramModification> currentAttempt = new();
          List<HashSet<Block>> infeasiblePaths = new();
          HashSet<ProgramModification> allPaths = new();
          node.ComputePredecessorsForBlocks();
          while (!allPathsFeasible) {
            currentAttempt.ForEach(modification => modification.ToBeIgnored = true);
            currentAttempt = new List<ProgramModification>();
            var newPaths = MinCover.GetMinCover(node, infeasiblePaths, minPaths);
            if (newPaths.Count == 0) {
              break;
            }
            minPaths = newPaths.Count;
            allPathsFeasible = true;
            foreach (var pathDescription in newPaths.OrderBy(p => p.Count)) {
              ProgramModification modification;
              var blockIds = pathDescription.Select(block => block.UniqueId).ToHashSet();
              var substitution = allPaths
                .Where(path =>
                  path.CounterexampleStatus ==
                  ProgramModification.Status.Success &&
                  path.coversBlocks.IsSupersetOf(blockIds))
                .FirstOrDefault((ProgramModification) null);
              if (substitution == null) {
                var path = new PathBasedModifier.Path(node,
                  pathDescription.Select(block => variables[block]),
                  returnBlocks);
                path.AssertPath();
                var name = ImplementationToTarget?.VerboseName ??
                           path.Impl.VerboseName;
                modification = ProgramModification.GetProgramModification(
                  program, path.Impl,
                  blockIds, new HashSet<string>(), name,
                  $"{name.Split(" ")[0]}(path through {string.Join(",", blockIds)})");
                path.NoAssertPath();
              } else {
                modification = substitution;
              }
              modification.ToBeIgnored = false;
              currentAttempt.Add(modification);
              allPaths.Add(modification);
              if (allPathsFeasible) {
                await modification.GetCounterExampleLog();
                if (!modification.IsCovered) {
                  infeasiblePaths.Add(pathDescription);
                  allPathsFeasible = false;
                }
              }
            }
            if (ProgramModification.NumberOfBlocksCovered(node) > bestResult) {
              bestResultPaths = new List<ProgramModification>();
              bestResultPaths.AddRange(currentAttempt);
            }
          }
          currentAttempt.ForEach(modification => modification.ToBeIgnored = true);
          bestResultPaths.ForEach(modification => modification.ToBeIgnored = false);
          foreach (var modification in bestResultPaths) {
            yield return modification;
          }
          break;
      }
    }

    private async IAsyncEnumerable<ProgramModification> VisitProgram(Program node) {
      program = node;
      foreach (var implementation in node.Implementations) {
        await foreach (var modification in VisitImplementation(implementation)) {
          yield return modification;
        }
      }
    }

    /// <summary>
    /// Return the list of all states covered by the block.
    /// A state is represented by the string recorded via :captureState
    /// </summary>
    private static HashSet<string> ExtractCapturedStates(Block node) {
      HashSet<string> result = new();
      foreach (var cmd in node.cmds) {
        if (!(cmd is AssumeCmd assumeCmd)) {
          continue;
        }
        if (assumeCmd.Attributes?.Key == "captureState") {
          result.Add(assumeCmd.Attributes?.Params?[0]?.ToString() ?? "");
        }
      }
      return result;
    }
  }
}