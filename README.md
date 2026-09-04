# RaceDay
A full-stack web-based event management system for the South African road running, walking, and cycling community. It replaces the paper-based registration and spreadsheet tracking that many local road events still rely on with a single system for
organising events and taking part in them.


## This is Part 1 of my three-part project for Programming 2B (PROG6212) in Diploma in Software Development at Rosebank Intenational.

## PART 1 Deliverables (/doc)
File descriptions
1.Raceday_erd.png which is the Entity Relationship Diagram, containing 6 entities (Users,
Events, Routes, Categories, Enrolments, Results) with
primary keys, foreign keys, and cardinality

2.Raceday_api_endpoint_plan.md which is the Full REST API endpoint specification (method, route,
description, role required, request body, expected
response)
3.raceday_database.sql which is SQL Server script that creates the full schema with
constraints, plus seed data

## Database Design
The data model centres on Users (holding both roles via a Role column), who create
Events . Each Event has one or more Categories (the individual races within it, e.g.
“10km Fun Run”) and Routes (the course details). The participants join a Category through
Enrolments , and each Enrolment can produce one Results record once the race is run.

## CI/CD
A GitHub Actions workflow validates that the repository structure is correct — specifically,
that the /docs folder exists and contains the ERD, API plan, and SQL script.
Build status: 

## Walkthrough Video
