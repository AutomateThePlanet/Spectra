# Workshop Setup Summary - AI in QA Modules 1-3

## Created Confluence Pages (CSP Space)

### 1. Service Request Management Requirements (CSP-001)
- **URL**: /spaces/CSP/pages/11337729/
- **Content**: Multi-step form requirements, validation rules, status management
- **Use in Modules**: Module 1 (manual test case generation), Module 2 (UI automation)

### 2. Advanced Search API Requirements (CSP-002)  
- **URL**: /spaces/CSP/pages/11370498/
- **Content**: Search API specifications, parameters, response formats, test scenarios
- **Use in Modules**: Module 3 (API testing and automation)

### 3. User Authentication and Management Requirements (CSP-003)
- **URL**: /spaces/CSP/pages/11403265/
- **Content**: Authentication flow, user roles, security requirements
- **Use in Modules**: Module 3 (API testing for auth endpoints)

## Created Jira Issues (CSRP Project)

### Epics (2)
- **CSRP-1**: Citizen service request submission system
- **CSRP-2**: Advanced search and filtering capabilities

### Stories (10) - Distributed by Team

#### Frontend Team (4 stories)
- **CSRP-3**: Implement multi-step service request form (8 points)
- **CSRP-4**: Create advanced search interface with filters (5 points)
- **CSRP-5**: Build responsive request list component (5 points)
- **CSRP-6**: Implement real-time status updates UI (3 points)

#### Backend Team (4 stories)
- **CSRP-7**: Develop service request CRUD API endpoints (8 points)
- **CSRP-8**: Implement advanced search API with pagination (8 points)
- **CSRP-9**: Create user authentication service (5 points)
- **CSRP-10**: Build request validation and sanitization layer (5 points)

#### Infrastructure Team (2 stories)
- **CSRP-11**: Set up database schema and migrations (5 points)
- **CSRP-12**: Configure API rate limiting and caching (5 points)

### Bugs (5) - Linked to Stories
- **CSRP-13**: Search API returns 500 error with special characters (High priority, linked to CSRP-8)
- **CSRP-14**: Pagination breaks when filtering by multiple statuses (High priority, linked to CSRP-8)
- **CSRP-15**: File upload fails for PDFs over 5MB (High priority, linked to CSRP-7)
- **CSRP-16**: Authentication token not refreshing properly (Urgent priority, linked to CSRP-9)
- **CSRP-17**: Form validation allows XSS in description field (Urgent priority, linked to CSRP-3)

### Tasks (3)
- **CSRP-18**: Create test data generation script
- **CSRP-19**: Document API endpoints in Swagger format
- **CSRP-20**: Set up monitoring and logging infrastructure

## Enhanced Prompt Templates

### Updated Files
1. **jira-item-creator-prompt.md** - Enhanced with:
   - Detailed story format template with team assignment, story points, acceptance criteria
   - Comprehensive bug format with environment, steps, severity, priority
   - Task template with deliverables and technical approach
   - Better examples aligned with created issues

2. **zephyr-test-creator-prompt.md** - Enhanced with:
   - API test case template for Module 3
   - UI automation test template for Module 2
   - Detailed test data and validation sections

## Module Exercise Coverage

### Module 1 - Manual Testing (30-35 min exercises)
✅ **Exercise 2**: Story + Requirements → Test Cases
- Use CSRP stories and Confluence pages
- Generate 5-10 test cases from requirements
- Focus on CSRP-3 (multi-step form) and CSRP-7 (CRUD APIs)

✅ **Exercise 3**: Manual Test Execution + AI Bug Report
- Test against existing bugs (CSRP-13 to CSRP-17)
- Generate bug reports using enhanced template
- Check for duplicates before creating

✅ **Exercise 4**: Support Ticket Analysis → Bug + Test Case
- Analyze bugs like CSRP-17 (XSS vulnerability)
- Create test cases for regression

### Module 2 - Web Test Automation (30-35 min exercises)
✅ **Exercise 2**: Refactor Legacy WebDriver Code
- Use CSRP-3 story requirements
- Apply Page Object Model
- Test multi-step form

✅ **Exercise 3**: Scaffold New Web Test Project
- Test search interface (CSRP-4)
- Test request list (CSRP-5)

✅ **Exercise 4**: Story + Requirements → Test Case + Java Test
- Use Confluence CSP-001 requirements
- Generate automated tests for form validation

### Module 3 - API Testing (30-35 min exercises)
✅ **Exercise 2**: Refactor Legacy RestAssured Test
- Use CSRP-7 (CRUD endpoints)
- Extract reusable methods

✅ **Exercise 3**: Scaffold API Test Project
- Use CSP-002 Advanced Search API requirements
- Generate tests from Swagger spec

✅ **Exercise 4**: Story + Requirements → API Test Scenarios
- Focus on CSRP-8 (search API) with multiple filters
- Test authentication (CSRP-9)
- Include test data setup using CSRP-18

## Key Points for Workshop Success

### Sufficient Content
✅ **3 Confluence pages** with detailed requirements
✅ **20 Jira issues** covering different types and complexities
✅ **Various priorities and severities** for realistic scenarios
✅ **Linked issues** showing relationships
✅ **Team distribution** for filtering exercises

### Exercise Suitability
- Requirements detailed enough for 10+ test cases
- APIs specified for Module 3 automation
- UI components defined for Module 2
- Bugs with clear reproduction steps
- Security issues for advanced testing

### MCP Integration Ready
- All content in Atlassian cloud
- Participants can fetch via MCP servers
- JQL queries will work with created issues
- Confluence pages linked to requirements

## Recommendations

1. **For Module 1**: Start with CSRP-3 (form story) as it has the most detailed requirements
2. **For Module 2**: Use CSRP-4 and CSRP-5 for UI automation practice
3. **For Module 3**: Focus on CSRP-8 (search API) as it has complex parameters perfect for API testing

## Next Steps

1. ✅ Test MCP connectivity with participant sandboxes
2. ✅ Verify JQL queries return expected results
3. ✅ Consider creating 1-2 example Zephyr test cases as templates
4. ✅ Prepare sample test data using CSRP-18 requirements

This setup should provide enough realistic content for all three modules while keeping documentation focused and manageable for 30-35 minute exercises.