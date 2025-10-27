# Array Mapping Manual Test Cases

This document provides comprehensive test cases for manually verifying the array mapping functionality in the dataset management system.

## Test Setup

1. Navigate to **Admin > Data > Add Dataset**
2. Fill in basic dataset information (name, origin, description, AI model type)
3. Use the JSONL files provided below for testing

## Test Case 1: Simple Array of Strings

### JSONL Input:
```json
{"tags": ["medical", "urgent", "followup"], "text": "Patient shows improvement", "priority": "high"}
{"tags": ["routine", "checkup"], "text": "Regular examination", "priority": "normal"}
{"tags": ["emergency", "critical", "immediate"], "text": "Immediate attention required", "priority": "critical"}
```

### Expected UI Behavior:
1. **Root Level**: Should show `tags`, `text`, `priority`
2. **Select `tags`**: Should show `0`, `1`, `2`, **`add array`**
3. **Select `add array`**: Should complete the path selection (no further options)
4. **Add as Input**: Should show `(array) Input 1: tags (text)`

### Expected Result:
- Each data object should have multiple input entries for tags
- First object: 3 tag inputs + 1 text input = 4 total inputs
- Second object: 2 tag inputs + 1 text input = 3 total inputs  
- Third object: 3 tag inputs + 1 text input = 4 total inputs

---

## Test Case 2: Array of Objects

### JSONL Input:
```json
{"patient_id": "P001", "images": [{"url": "xray1.jpg", "type": "chest", "quality": "good"}, {"url": "xray2.jpg", "type": "lateral", "quality": "excellent"}], "diagnosis": "normal"}
{"patient_id": "P002", "images": [{"url": "ct1.jpg", "type": "head", "quality": "fair"}], "diagnosis": "abnormal"}
```

### Expected UI Behavior:
1. **Root Level**: Should show `patient_id`, `images`, `diagnosis`
2. **Select `images`**: Should show `0`, `1`, **`add array`**
3. **Select `add array`**: Should show `url`, `type`, `quality`
4. **Select `url`**: Path complete
5. **Add as Input**: Should show `(array) Input 1: images.url (imageurl)`

### Expected Result:
- First object: 2 image URLs + 1 patient_id = 3 inputs
- Second object: 1 image URL + 1 patient_id = 2 inputs

---

## Test Case 3: Nested Arrays (Current Limitation Test)

### JSONL Input:
```json
{"data": {"items": [{"urls": ["url1", "url2", "url3"], "metadata": "data1"}, {"urls": ["url4", "url5"], "metadata": "data2"}]}}
```

### Expected UI Behavior:
1. **Root Level**: Should show `data`
2. **Select `data`**: Should show `items`
3. **Select `items`**: Should show `0`, `1`, **`add array`**
4. **Select `add array`**: Should show `urls`, `metadata`
5. **Select `urls`**: Path complete

### Expected Result (Current Limitation):
- Should create 2 inputs containing JSON strings of the nested arrays
- Input 1: `["url1", "url2", "url3"]`
- Input 2: `["url4", "url5"]`

**Note**: This demonstrates the current limitation where nested arrays are not recursively processed.

---

## Test Case 4: Mixed Array and Regular Mappings

### JSONL Input:
```json
{"title": "Medical Report", "authors": [{"name": "Dr. Smith", "email": "smith@hospital.com"}, {"name": "Dr. Jones", "email": "jones@hospital.com"}], "content": "Patient examination results", "tags": ["cardiology", "routine"]}
```

### Expected UI Behavior:
**For Input Mappings:**
1. Add `title` as regular mapping: `Input 1: title (text)`
2. Add `authors` → `add array` → `name`: `(array) Input 2: authors.name (text)`
3. Add `tags` → `add array`: `(array) Input 3: tags (text)`

**For Output Mappings:**
1. Add `content` as regular mapping: `Output 1: content (text)`
2. Add `authors` → `add array` → `email`: `(array) Output 2: authors.email (text)`

### Expected Result:
- Inputs: 1 title + 2 author names + 2 tags = 5 total
- Outputs: 1 content + 2 author emails = 3 total

---

## Test Case 5: Deep Nesting

### JSONL Input:
```json
{"hospital": {"departments": {"radiology": {"equipment": [{"name": "MRI-1", "status": "active"}, {"name": "CT-2", "status": "maintenance"}]}}}}
```

### Expected UI Behavior:
1. **Root Level**: `hospital`
2. **Select `hospital`**: `departments`
3. **Select `departments`**: `radiology`
4. **Select `radiology`**: `equipment`
5. **Select `equipment`**: `0`, `1`, **`add array`**
6. **Select `add array`**: `name`, `status`
7. **Select `name`**: Path complete

### Expected Result:
- Should create 2 inputs: "MRI-1", "CT-2"

---

## Test Case 6: Empty Arrays and Error Handling

### JSONL Input:
```json
{"tags": [], "title": "No tags example", "items": [{"valid": "data"}, {"missing_field": "test"}]}
{"tags": ["single"], "title": "One tag example"}
```

### Expected UI Behavior:
1. Empty arrays should still show **`add array`** option
2. Missing fields in array objects should be handled gracefully

### Expected Result:
- First object: Only title input (empty array produces no inputs)
- Second object: 1 tag + 1 title = 2 inputs

---

## Test Case 7: Array Display and Identification

### Expected UI Indicators:
1. **Dropdown Options**: Array items should show "(Array)" suffix
2. **Add Array Option**: Should appear as "add array (Add Array)"
3. **Mapping Display**: Should show "(array)" prefix: `(array) Input 1: tags (text)`
4. **Reset Behavior**: After adding array mapping, form should reset for next mapping

---

## Validation Checklist

For each test case, verify:

### ✅ Frontend Behavior:
- [ ] Correct dropdown options appear at each level
- [ ] "add array" option appears for arrays
- [ ] Array mappings show "(array)" prefix
- [ ] UI resets properly after adding mappings
- [ ] Can mix array and regular mappings

### ✅ Backend Processing:
- [ ] Correct number of data objects created
- [ ] Each object has expected number of input/output items
- [ ] Array values are properly extracted
- [ ] Non-array mappings work alongside array mappings
- [ ] Missing values in arrays are handled gracefully

### ✅ Data Integrity:
- [ ] All expected content is preserved
- [ ] No data loss during processing
- [ ] Token counts are calculated correctly
- [ ] Image URLs are processed properly when specified

---

## Performance Testing

### Large Array Test:
```json
{"items": [/* 100+ objects with multiple fields */]}
```

### Expected Behavior:
- UI should remain responsive
- Backend should process in batches
- Memory usage should remain reasonable

---

## Error Scenarios

### Invalid JSON:
```json
{"tags": ["item1", "item2"]} // Missing closing bracket
```

### Mixed Types:
```json
{"mixed": ["string", 123, null, {"nested": "object"}]}
```

### Deeply Nested:
```json
{"level1": {"level2": {"level3": {"level4": {"items": [{"deep": "value"}]}}}}}
```

---

## Expected Limitations

1. **Nested Arrays**: Currently converts to JSON strings rather than recursive processing
2. **Complex Objects**: Large objects in arrays may affect performance
3. **Memory**: Very large arrays may require pagination in future versions

---

## Regression Testing

After implementing array mapping, verify these existing features still work:

- [ ] Regular (non-array) key selection
- [ ] Image URL processing
- [ ] File upload and processing
- [ ] Dataset editing and deletion
- [ ] Data object viewing and exploration

---

## Notes for Developers

- Test with various JSONL file sizes (small, medium, large)
- Verify error handling with malformed JSON
- Check browser developer console for any JavaScript errors
- Monitor network requests during file upload and processing
- Test on different browsers (Chrome, Firefox, Safari, Edge) 