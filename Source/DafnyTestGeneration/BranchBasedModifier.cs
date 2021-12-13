using System.Collections.Generic;
using Microsoft.Boogie;

namespace DafnyTestGeneration {

  /// <summary>
  /// A version of ProgramModifier that inserts assertions into the code
  /// that fail when a particular branch is taken
  /// </summary>
  public class BranchBasedModifier : ProgramModifier {

    public class Vertex {
      public void GenerateDotFile() {
        HashSet<string> lines = new HashSet<string>();
        lines.Add("digraph foograph {");
        addDotLines(lines);
        lines.Add("}");
        System.IO.File.WriteAllLines("output.dot", lines);
      }

      private bool visited = false;
      private void addDotLines(HashSet<string> lines) {
        if (this.visited) {
          return;
        }
        this.visited = true;

        foreach (Edge e in outgoing) {
          if (e.tail != null) {
            lines.Add(e.head.getGraphName() + " -> " + e.tail.getGraphName());
            e.tail.addDotLines(lines);
          }
          else {
            lines.Add(e.head.data.Label+"_"+e.head.data.UniqueId + " -> " + "return");
            lines.Add(e.head.getGraphName() + " -> return");
          }
        }
      }

      private string getGraphName() {
        return data.Label + "_" + data.UniqueId + (removed ? "_Removed" : "");
      }
      public Vertex(Block blockIn) {
        data = blockIn;
        incoming = new HashSet<Edge>();
        outgoing = new HashSet<Edge>();
        removed = false;
      }

      public Block data;
      public HashSet<Edge> incoming;
      public HashSet<Edge> outgoing;

      public bool removed;
    }
    public class Edge {

      public Edge(Vertex headIn) {
        head = headIn;
        tail = null;
        visited = false;
      }
      public Vertex head;
      public Vertex tail;
      public bool visited;

      public override bool Equals(object obj) {
        return obj is Edge edge &&
               EqualityComparer<Vertex>.Default.Equals(head, edge.head) &&
               EqualityComparer<Vertex>.Default.Equals(tail, edge.tail);
      }

      public override int GetHashCode() {
        return System.HashCode.Combine(head, tail, visited);
      }
    }


    // prefix given to variables indicating whether or not a block was visited
    private const string blockVarNamePrefix = "$$visited$$_";

    private Vertex graph = null;
    private Vertex returnVertex = null;
    private List<Path> paths = new();

    private ProgramModification GenerateModifcation(Program p, Path path) {
        ProgramModification result;
        path.AssertPath();
        result = new ProgramModification(p, ProcedureName ?? path.Impl.Name);
        path.NoAssertPath();
        return result;
    }

    private bool PathIsFeasible(ProgramModification pm) {
        if(pm.GetCounterExampleLog() == null) {
          return false;
        }
        else {
          return true;
        }
    }
    private Program program = null;
    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      this.program = p;
      paths = new List<Path>();
      var result = new List<ProgramModification>();
      p = VisitProgram(p); // populates paths
      foreach (var path in paths) {
        System.Console.WriteLine(path);
        result.Add(GenerateModifcation(p, path));
      }
      return result;
    }

    /// <summary>
    /// Insert variables to register which blocks are visited
    /// and then populate the paths field.
    /// </summary>
    public override Implementation VisitImplementation(Implementation node) {
      if (!ProcedureIsToBeTested(node.Name)) {
        return node;
      }
      InitBlockVars(node);
      var blockNameToId = GetIdToBlock(node);
      var blockToVertex = new Dictionary<int, Vertex>();
      System.Console.WriteLine("GENERATING GRAPH");
      GenerateGraph(node, blockNameToId, blockToVertex , node.Blocks[0], null);
      System.Console.WriteLine("GENERATING DOT");
      graph.GenerateDotFile();
      System.Console.WriteLine("GENERATING PATHS");
      GeneratePaths(node,
        blockNameToId,
        blockToVertex,
        node.Blocks[0],
        null,
        new HashSet<int>(),
        new List<int>());
      return node;
    }

    /// <summary>
    /// Create a map from block ids (aka labels) to blocks themselves
    /// </summary>
    private static Dictionary<string, Block> GetIdToBlock(Implementation impl) {
      var result = new Dictionary<string, Block>();
      foreach (var block in impl.Blocks) {
        result[block.Label] = block;
      }
      return result;
    }

    /// <summary>
    /// Modify implementation by adding variables indicating whether or not
    /// certain blocks were visited.
    /// </summary>
    private static void InitBlockVars(Implementation node) {
      foreach (var block in node.Blocks) {
        var var = blockVarNamePrefix + block.UniqueId;
        // variable declaration:
        node.LocVars.Add(new LocalVariable(new Token(),
          new TypedIdent(new Token(), var, Type.Bool)));
        // initialization:
        block.cmds.Insert(0, GetCmd($"{var} := true;", returns: $"{var}:bool"));
        // set variable to true when visiting a block
        node.Blocks[0].cmds.Insert(0, GetCmd(
          $"var {var}:bool; {var} := false;"));
      }
    }

    /// <summary>
    /// Build a graph that shows which basic blocks are reachable from other basic blocks
    /// </summary>
    /// <param name="impl">The root node (implementation) to generate a graph for</param>
    /// <param name="idToBlock">maps block ids to blocks</param>
    /// <param name="blockToVertex">maps block ids to the Vertex in the graph</param>
    /// <param name="block">block with which to start AST traversal</param>
    /// <param name="currSet">set of block already inside the path</param>
    /// <param name="currList">the blocks forming the path</param>
    private void GenerateGraph(Implementation impl,
      Dictionary<string, Block> idToBlock, Dictionary<int, Vertex> blockToVertex, Block block, Edge pred
      ) {
      //System.Console.Write("Visiting Block:");
      //System.Console.WriteLine(block);
      //System.Console.WriteLine(block.Predecessors.Count); ALWAYS ZERO! THAT IS ANOYING!
      bool isNew = true;
      Vertex current;
      if (blockToVertex.ContainsKey(block.UniqueId)) {
        current = blockToVertex[block.UniqueId];
        isNew = false;
      }
      else {
        current = new Vertex(block);
        blockToVertex.Add(block.UniqueId, current);
      }
      if (pred != null) {
        pred.tail = current;
        current.incoming.Add(pred);
      }
      else { //if we have no predecessor we are the head of the graph
        graph = current;
        returnVertex = new Vertex(null);
      }
      if (!isNew) return;
      // if the block contains a return command, it is the last one in the path:
      if (block.TransferCmd is ReturnCmd) {
        Edge succ = new Edge(current);
        succ.tail = null;
        current.outgoing.Add(succ);
        return;
      }

      // otherwise, each goto statement presents a new path to take:
      var gotoCmd = block.TransferCmd as GotoCmd;
      foreach (var b in gotoCmd?.labelNames ?? new List<string>()) {
        Edge succ = new Edge(current);
        current.outgoing.Add(succ);
        GenerateGraph(impl, idToBlock, blockToVertex, idToBlock[b], succ);
      }
    }


    /// <summary>
    /// Populate paths field with paths generated for the given implementation
    /// </summary>
    /// <param name="impl">implementation to generate paths for</param>
    /// <param name="idToBlock">maps block ids to blocks</param>
    /// <param name="block">block with which to start AST traversal</param>
    /// <param name="currSet">set of block already inside the path</param>
    /// <param name="currList">the blocks forming the path</param>
    private bool GeneratePaths(Implementation impl,
      Dictionary<string, Block> idToBlock, Dictionary<int, Vertex> blockToVertex, Block block, Edge pred,
      HashSet<int> currSet, List<int> currList) {
      if (currSet.Contains(block.UniqueId)) {
        //System.Console.Write("Allready Visited:");
        //System.Console.WriteLine(block.UniqueId);
        return false; //This only happens if there is a cycle
      }

      int unvisitedEdges = 0;
      foreach (Edge e in blockToVertex[block.UniqueId].incoming) {
        if (!e.visited) {
          if (!e.Equals(pred)) {
            unvisitedEdges++; 
          }
        }
      }
      //System.Console.Write("UNVISITED INCOING EDGES ");
      //System.Console.Write(block.Label);
      //System.Console.Write(": ");
      //System.Console.Write(unvisitedEdges);
      //System.Console.Write("/");
      //System.Console.WriteLine(blockToVertex[block.UniqueId].incoming.Count);

      // if the block contains a return command, it is the last one in the path:
      if (block.TransferCmd is ReturnCmd) {
        Path newPath = new Path(impl, currList, block);
        if (PathIsFeasible(GenerateModifcation(program, newPath))) {
          System.Console.WriteLine("Found a new FEASIBLE PATH");
          paths.Add(newPath);
          return true;
        }
        else {
          System.Console.WriteLine("PATH WAS INFEASIBLE");
          return false;
        }
        //TODO FIND HOW TO GET Program p here so I can check if it is feaisble.
      }

      // otherwise, each goto statement presents a new path to take:
      currSet.Add(block.UniqueId);
      currList.Add(block.UniqueId);
      var gotoCmd = block.TransferCmd as GotoCmd;
      bool hasFeasiblePath = false;
      foreach (var b in gotoCmd?.labelNames ?? new List<string>()) {
        Edge edge = null;
        foreach (var e in blockToVertex[block.UniqueId].outgoing) {
          if (e.tail == blockToVertex[idToBlock[b].UniqueId]) {
            edge = e;
          }
        }
        if (edge == null) {
          throw new KeyNotFoundException(b);
        }
        if (edge.visited) {
          continue;
        }
        bool pathIsFeasible = GeneratePaths(impl, idToBlock, blockToVertex, idToBlock[b], edge, currSet, currList);
        if (pathIsFeasible) {
          hasFeasiblePath = true;
        }
        if (pathIsFeasible && unvisitedEdges > 0) {
          //System.Console.WriteLine("Will come back to:" + b);
          break;
        }
        /*
        else {
          System.Console.WriteLine("Last Visit to:" + b);
        }
        */
      }
      if (hasFeasiblePath) {
        foreach (Edge e in blockToVertex[block.UniqueId].incoming) {
          if (!e.visited) {
            if (e.Equals(pred)) {
              //because we may have visited this vertex multiple times and incoming and outgoing are sets e.Equals(pred) does not imply e == pred
              e.visited = true;
            }
          }
        }
      }
      currList.RemoveAt(currList.Count - 1);
      currSet.Remove(block.UniqueId);
      return hasFeasiblePath;
    }

    private class Path {

      public readonly Implementation Impl;
      private readonly List<int> path; // indices of blocks along the path
      private readonly Block returnBlock; // block where the path ends

      internal Path(Implementation impl, IEnumerable<int> path, Block returnBlock) {
        Impl = impl;
        this.path = new List<int>();
        this.path.AddRange(path); // deepcopy is necessary here
        this.returnBlock = returnBlock;
      }

      public override bool Equals(object? obj) {
        return obj is Path path &&
               EqualityComparer<Implementation>.Default.Equals(Impl, path.Impl) &&
               EqualityComparer<List<int>>.Default.Equals(this.path, path.path) &&
               EqualityComparer<Block>.Default.Equals(returnBlock, path.returnBlock);
      }

      public override string ToString() {
        string str = "[";
        foreach (int i in this.path) {
          str += i + ", ";
        }
        str += this.returnBlock.UniqueId;
        str += ", return]";
        return str;
      }

      internal void AssertPath() {
        if (path.Count == 0) {
          returnBlock.cmds.Add(GetCmd("assert false;"));
          return;
        }

        var vars = path.ConvertAll(x => blockVarNamePrefix + x);
        var varsCond = string.Join("||", vars.ConvertAll(x => $"!{x}"));
        // The only purpose of varsIn is to make a call to GetCmd possible
        var varsIn = string.Join(", ", vars.ConvertAll(x => $"{x}:bool"));
        returnBlock.cmds.Add(GetCmd($"assert {varsCond};", varsIn));
      }

      internal void NoAssertPath() {
        returnBlock.cmds.RemoveAt(returnBlock.cmds.Count - 1);
      }
    }
  }
}