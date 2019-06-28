using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace ZefieLib
{
    public class TreeNode
    {
        /// <summary>
        /// Creates nodes and child nodes based on a string path
        /// </summary>
        /// <param name="n">The node create children on</param>
        /// <param name="path">TreeNode.FullPath-style string</param>
        /// <param name="backColor">Background color of generated nodes</param>
        /// <param name="foreColor">Text color of generated nodes</param>
        /// <returns>The last child node created</returns>
        public static string CreateNodesByPath(System.Windows.Forms.TreeNode n, string path, Color? backColor = null, Color? foreColor = null)
        {
            List<String> p = new List<string>();
            foreach (string s in path.Split('\\'))
                p.Add(s);

            p.RemoveAt(p.Count - 1);
            for (int i = 0; i < p.Count; i++)
            {
                if (i == 0)
                {
                    if (!n.Nodes.ContainsKey(p[i]))
                    {
                        System.Windows.Forms.TreeNode tmp = new System.Windows.Forms.TreeNode(p[i])
                        {
                            Name = p[i]
                        };
                        if (backColor != null) { tmp.BackColor = (Color)backColor; }
                        if (foreColor != null) { tmp.ForeColor = (Color)foreColor; }
                        n.Nodes.Add(tmp);
                    }
                }
                else
                {
                    if (!n.Nodes.Find(p[i - 1], true)[0].Nodes.ContainsKey(p[i]))
                    {
                        System.Windows.Forms.TreeNode tmp = new System.Windows.Forms.TreeNode(p[i])
                        {
                            Name = p[i]
                        };
                        if (backColor != null) { tmp.BackColor = (Color)backColor; }
                        if (foreColor != null) { tmp.ForeColor = (Color)foreColor; }
                        n.Nodes.Find(p[i - 1], true)[0].Nodes.Add(tmp);
                    }
                }
            }
            return p[p.Count() - 1];
        }
    }
}
