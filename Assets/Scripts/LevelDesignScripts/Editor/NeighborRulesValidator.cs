using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
/// <summary>
/// Utility to validate and auto-fix NeighborRulesConfig for WFC consistency
/// Add this to your Editor folder
/// </summary>
public class NeighborRulesValidator : EditorWindow
{
    private NeighborRulesConfig rulesConfig;
    private Vector2 scrollPos;
    private bool showDetails = false;

    [MenuItem("Tools/Level Generator/Validate Neighbor Rules")]
    static void Init()
    {
        NeighborRulesValidator window = (NeighborRulesValidator)EditorWindow.GetWindow(typeof(NeighborRulesValidator));
        window.titleContent = new GUIContent("Rules Validator");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Neighbor Rules Validator", EditorStyles.boldLabel);

        rulesConfig = (NeighborRulesConfig)EditorGUILayout.ObjectField("Rules Config", rulesConfig, typeof(NeighborRulesConfig), false);

        if (rulesConfig == null)
        {
            EditorGUILayout.HelpBox("Select a NeighborRulesConfig asset to validate", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Rules", GUILayout.Height(30)))
        {
            ValidateRules();
        }

        if (GUILayout.Button("Auto-Fix Missing Bidirectional Rules", GUILayout.Height(30)))
        {
            AutoFixBidirectionalRules();
        }

        if (GUILayout.Button("Make All Rules Symmetric", GUILayout.Height(30)))
        {
            MakeRulesSymmetric();
        }

        EditorGUILayout.Space();
        showDetails = EditorGUILayout.Foldout(showDetails, "Rule Details");

        if (showDetails)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            ShowRuleDetails();
            EditorGUILayout.EndScrollView();
        }
    }

    void ValidateRules()
    {
        if (rulesConfig.catalog == null)
        {
            Debug.LogError("NeighborRulesConfig.catalog is null! Cannot validate.");
            return;
        }

        Debug.Log("=== NEIGHBOR RULES VALIDATION ===");

        int warnings = 0;
        int errors = 0;

        // Check each surface rule
        foreach (var rule in rulesConfig.surfaceRules)
        {
            if (string.IsNullOrEmpty(rule.selfID))
            {
                Debug.LogError($"Found rule with empty selfID!");
                errors++;
                continue;
            }

            // Check if selfID exists in catalog
            var selfDef = rulesConfig.catalog.GetByID(rule.selfID);
            if (selfDef == null)
            {
                Debug.LogError($"Rule for '{rule.selfID}' but no PrefabDef exists with that ID!");
                errors++;
                continue;
            }

            // Check for bidirectional rules
            var directions = new[] {
                NeighborRulesConfig.DirectionMask.Forward,
                NeighborRulesConfig.DirectionMask.Backward,
                NeighborRulesConfig.DirectionMask.Left,
                NeighborRulesConfig.DirectionMask.Right,
                NeighborRulesConfig.DirectionMask.Corners  // Add corners check
            };

            foreach (var allowed in rule.allowed)
            {
                if (string.IsNullOrEmpty(allowed.neighborID)) continue;

                // Check if neighbor exists
                var neighborDef = rulesConfig.catalog.GetByID(allowed.neighborID);
                if (neighborDef == null)
                {
                    Debug.LogWarning($"Rule '{rule.selfID}' allows '{allowed.neighborID}' but no such PrefabDef exists!");
                    warnings++;
                    continue;
                }

                // Check if bidirectional
                foreach (var dir in directions)
                {
                    if ((allowed.directions & dir) != 0)
                    {
                        // This direction is allowed, check reverse
                        var reverseDir = GetReverseDirection(dir);

                        // Find rule for neighbor
                        var neighborRule = rulesConfig.surfaceRules.FirstOrDefault(r => r.selfID == allowed.neighborID);
                        if (neighborRule != null)
                        {
                            // Check if neighbor allows self in reverse direction
                            bool hasReverse = neighborRule.allowed.Any(a =>
                                a.neighborID == rule.selfID &&
                                (a.directions & reverseDir) != 0);

                            if (!hasReverse)
                            {
                                Debug.LogWarning($"Asymmetric rule: '{rule.selfID}' → '{allowed.neighborID}' ({dir}) exists, but reverse '{allowed.neighborID}' ← '{rule.selfID}' ({reverseDir}) is missing!");
                                warnings++;
                            }
                        }
                    }
                }
            }
        }

        Debug.Log($"\n=== VALIDATION COMPLETE ===");
        Debug.Log($"Errors: {errors}");
        Debug.Log($"Warnings: {warnings}");

        if (errors == 0 && warnings == 0)
        {
            Debug.Log("<color=green>✓ All rules are valid and symmetric!</color>");
        }
        else if (errors == 0)
        {
            Debug.LogWarning($"⚠ {warnings} warnings found. Consider using Auto-Fix.");
        }
        else
        {
            Debug.LogError($"✗ {errors} errors found. Fix these before using the rules!");
        }
    }

    void AutoFixBidirectionalRules()
    {
        if (!EditorUtility.DisplayDialog("Auto-Fix Rules",
            "This will add missing reverse direction rules to make all constraints bidirectional. Continue?",
            "Yes", "Cancel"))
        {
            return;
        }

        Undo.RecordObject(rulesConfig, "Auto-Fix Bidirectional Rules");

        int added = 0;

        // For each rule
        foreach (var rule in rulesConfig.surfaceRules)
        {
            foreach (var allowed in rule.allowed)
            {
                if (string.IsNullOrEmpty(allowed.neighborID)) continue;

                // Find the neighbor's rule
                var neighborRule = rulesConfig.surfaceRules.FirstOrDefault(r => r.selfID == allowed.neighborID);

                if (neighborRule == null)
                {
                    // Create new rule for neighbor
                    neighborRule = new NeighborRulesConfig.NeighborEntry { selfID = allowed.neighborID };
                    rulesConfig.surfaceRules.Add(neighborRule);
                }

                // Check each direction
                var directions = new[] {
                    NeighborRulesConfig.DirectionMask.Forward,
                    NeighborRulesConfig.DirectionMask.Backward,
                    NeighborRulesConfig.DirectionMask.Left,
                    NeighborRulesConfig.DirectionMask.Right
                };

                foreach (var dir in directions)
                {
                    if ((allowed.directions & dir) != 0)
                    {
                        var reverseDir = GetReverseDirection(dir);

                        // Check if reverse exists
                        var reverseConstraint = neighborRule.allowed.FirstOrDefault(a => a.neighborID == rule.selfID);

                        if (reverseConstraint == null)
                        {
                            // Add new constraint
                            reverseConstraint = new NeighborRulesConfig.NeighborConstraint
                            {
                                neighborID = rule.selfID,
                                directions = reverseDir,
                                weight = allowed.weight
                            };
                            neighborRule.allowed.Add(reverseConstraint);
                            added++;
                        }
                        else if ((reverseConstraint.directions & reverseDir) == 0)
                        {
                            // Add missing direction
                            reverseConstraint.directions |= reverseDir;
                            added++;
                        }
                    }
                }
            }
        }

        EditorUtility.SetDirty(rulesConfig);

        Debug.Log($"<color=green>✓ Auto-fix complete! Added {added} reverse rules.</color>");
    }

    void MakeRulesSymmetric()
    {
        if (!EditorUtility.DisplayDialog("Make Rules Symmetric",
            "This will change ALL directional rules to use 'All' directions (Forward|Backward|Left|Right). This ensures complete symmetry but may be overly permissive. Continue?",
            "Yes", "Cancel"))
        {
            return;
        }

        Undo.RecordObject(rulesConfig, "Make Rules Symmetric");

        foreach (var rule in rulesConfig.surfaceRules)
        {
            foreach (var allowed in rule.allowed)
            {
                allowed.directions = NeighborRulesConfig.DirectionMask.All;
            }
        }

        EditorUtility.SetDirty(rulesConfig);

        Debug.Log("<color=green>✓ All rules now use All directions!</color>");
    }

    NeighborRulesConfig.DirectionMask GetReverseDirection(NeighborRulesConfig.DirectionMask dir)
    {
        switch (dir)
        {
            case NeighborRulesConfig.DirectionMask.Forward: return NeighborRulesConfig.DirectionMask.Backward;
            case NeighborRulesConfig.DirectionMask.Backward: return NeighborRulesConfig.DirectionMask.Forward;
            case NeighborRulesConfig.DirectionMask.Left: return NeighborRulesConfig.DirectionMask.Right;
            case NeighborRulesConfig.DirectionMask.Right: return NeighborRulesConfig.DirectionMask.Left;
            case NeighborRulesConfig.DirectionMask.Corners: return NeighborRulesConfig.DirectionMask.Corners; // Corners reverse to corners
            default: return NeighborRulesConfig.DirectionMask.None;
        }
    }

    void ShowRuleDetails()
    {
        EditorGUILayout.LabelField("Surface Rules:", EditorStyles.boldLabel);

        foreach (var rule in rulesConfig.surfaceRules)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Self: {rule.selfID}", EditorStyles.boldLabel);

            if (rule.allowed.Count > 0)
            {
                EditorGUILayout.LabelField("Allowed:");
                EditorGUI.indentLevel++;
                foreach (var a in rule.allowed)
                {
                    EditorGUILayout.LabelField($"→ {a.neighborID} ({a.directions}) weight={a.weight}");
                }
                EditorGUI.indentLevel--;
            }

            if (rule.denied.Count > 0)
            {
                EditorGUILayout.LabelField("Denied:");
                EditorGUI.indentLevel++;
                foreach (var d in rule.denied)
                {
                    EditorGUILayout.LabelField($"✗ {d.neighborID} ({d.directions})");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif