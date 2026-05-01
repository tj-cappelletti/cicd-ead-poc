# JSON Schema Versioning Standard

## Decision
We have set a standard for all event payload schemas to use **JSON Schema Draft-07** in this repository.

## Rationale

### 1. **Industry Standard for Event Systems**
Draft-07 is the de facto standard adopted by major event and messaging specifications:
- **CloudEvents**: The CNCF standard for describing events uses Draft-07
- **AsyncAPI**: The specification for asynchronous APIs standardizes on Draft-07
- This ensures our schemas are compatible with ecosystem tools and validators

### 2. **Universal Tooling Support**
- Universally supported across all programming languages and platforms
- Mature, stable validator implementations in JavaScript, Python, Java, Go, C#, etc.
- Better backward compatibility with older systems that may consume our events
- No risk of version-specific tooling gaps or incompatibilities

### 3. **Sufficient Feature Coverage**
Draft-07 provides all features needed for our event architecture:
- `required`, `enum`, `pattern`, `format` for validation
- `additionalProperties` for strict typing
- Nested schema definitions for complex structures like resource objects
- `additionalProperties: true` for extensible details objects
- No limitations for our current or foreseeable use cases

### 4. **Simplified Maintenance**
- Clearer specifications and wider documentation
- Easier for team members to understand and debug
- Reduced cognitive load when reviewing schemas
- Better compatibility with community examples and best practices

### 5. **Pragmatic Evolution Path**
- If future requirements demand features from Draft 2020-12, we can upgrade selectively
- Draft-07 serves as a stable foundation; newer drafts add advanced features, not core functionality
- Upgrading is a low-risk, opt-in decision rather than a mandatory overhaul

## Schema Location Reference
Draft-07 schemas use the following identifier:
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#"
}
```

## Exceptions
No exceptions are permitted without explicit team consensus and documented rationale in a separate decision record.
