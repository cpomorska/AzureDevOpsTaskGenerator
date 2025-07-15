# TODOs for AzureDevOpsTaskGenerator

## Parser & Field Mapping Improvements

- [ ] **Refactor MarkdownTaskParser effort extraction:**
      - Ensure feature and task effort is always correctly extracted from indented and property lines.
      - Add unit tests for ExtractEffortFromLine to cover all bullet/property line formats (e.g., '- **Effort**: 8 SP', '- 8 SP', '- 8 story points').
- [ ] **Improve Business Value extraction:**
      - Support mapping of both string and numeric values, and add tests for edge cases.
- [ ] **Priority mapping robustness:**
      - Ensure that all values sent to Azure DevOps are valid (1, 2, 3, 4) and never default to 0.
      - Add validation logic and tests for unknown/invalid priorities.
- [ ] **Implement User Story work item creation:**
      - Support parsing and creation of User Story work items from markdown input.
      - Ensure correct field mapping and parent/child relationships for User Stories in Azure DevOps.
      - Add tests for User Story parsing and creation logic.

## Integration & Error Handling

- [ ] **Azure DevOps error feedback:**
      - Improve error messages when field values are invalid or missing.
      - Log which field and value caused the error for easier debugging.
- [ ] **Test with real Azure DevOps instance:**
      - Validate that all field mappings work as expected with a live project.

## Documentation

- [ ] **Update README:**
      - Document how Business Value and Priority are mapped.
      - Add a table of supported field values and their mappings.
      - Explain how to add new mappings for custom fields.
