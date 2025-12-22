# Service Management DOM Link Configuration Versions to Services

## About

This update enhances the **Service Management DOM** by adding a reference list field to the Service DOM Definition, linking it to all associated Service Configuration Versions.
For existing Services, this field will be populated by tracing the assigned configuration and following its chain of previous references through to the final version.

## Key Features

- **Update DOM Model**  
  Introduces a Configuration Versions reference list field in the Service DOM definition, enabling direct linkage to all related Service Configuration Versions.

- **Service Data Migration**  
  Automatically updates existing Services to include references to their associated Service Configuration Versions, ensuring consistency, traceability, and maintainability.

- **Enhanced Configuration Management**
  Simplifies transitions between different configurations, making Service updates more flexible and reducing operational complexity.