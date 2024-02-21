## Attributes 

### [ReadOnlyAttribute](Attributes/Runtime/ReadOnlyAttribute.cs)
Creates an immutable variable in the inspector.
```csharp
[ReadOnly] public int variable; 
```

### [RenameAttribute](Attributes/Runtime/RenameAttribute.cs)
Renames a variable in the inspector.
```csharp
[Rename("More Specific Name"), SerializeField] private int variable; 
```