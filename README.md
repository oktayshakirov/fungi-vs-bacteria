## Configure Unity YAML Merge (Smart Merge)

#### 1. Locate UnityYAMLMerge:

UnityYAMLMerge is included with your Unity installation. On macOS, you can find it at:

```
/Applications/Unity/Hub/Editor/[UnityVersion]/Unity.app/Contents/Tools/UnityYAMLMerge
```

Replace [UnityVersion] with your specific Unity version, e.g., 6000.0.25f1.

#### 2. Modify/Add the merge section in your `.git/config` file:

```
[merge "unityyamlmerge"]
    name = Unity SmartMerge (UnityYAMLMerge)
    driver = /Applications/Unity/Hub/Editor/[UnityVersion]/Unity.app/Contents/Tools/UnityYAMLMerge merge -p %O %A %B %A
    recursive = binary
```

#### 3. Create or Modify .gitattributes:

In your project's root directory, create or edit the .gitattributes file to specify which files should use the custom merge driver:

```
# Unity YAML files
*.unity  merge=unityyamlmerge
*.prefab merge=unityyamlmerge
*.asset  merge=unityyamlmerge
*.meta   merge=unityyamlmerge
```

This configuration tells Git to use UnityYAMLMerge for merging the specified file types.

#### 4. Configure Unity Project Settings:

Within Unity, adjust your project settings to facilitate better version control integration:

- Navigate to `Edit > Project Settings > Editor`.
- Set `Version Control mode` to `Visible Meta Files`.
- Set `Asset Serialization mode` to `Force Text`.

These settings ensure that Unity assets are stored in a text-based format, making them more suitable for version control systems.

#### 5. Handling Merge Conflicts:

When a merge conflict occurs:

- Run `git mergetool` in your terminal.
- Git will invoke UnityYAMLMerge for the specified file types.
- If UnityYAMLMerge cannot automatically resolve a conflict, it will prompt you to resolve it manually.
