-- Part 2 additions to the Part 1 schema (docs/schema.sql).
--
-- The Part 2 brief says every event must capture a name, description, date, location,
-- distance and event type (run, walk or cycle). The Part 1 ERD stores distance on Routes and
-- Categories and has no event type, so two columns were added to dbo.Events.
-- Run this against RaceDayDB if you create the database from your Part 1 script instead of
-- from the EF Core migrations, and update your ERD to show the two new Events columns.

USE RaceDayDB;
GO

IF COL_LENGTH('dbo.Events', 'EventType') IS NULL
BEGIN
    ALTER TABLE dbo.Events
        ADD EventType VARCHAR(20) NOT NULL
            CONSTRAINT DF_Events_EventType DEFAULT 'Run'
            CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle'));
END
GO

IF COL_LENGTH('dbo.Events', 'DistanceKM') IS NULL
BEGIN
    ALTER TABLE dbo.Events
        ADD DistanceKM DECIMAL(6,2) NOT NULL
            CONSTRAINT DF_Events_DistanceKM DEFAULT 0;
END
GO
