-- Check if the database exists, and create it if it doesn't
IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

-- Drop tables if they already exist, in FK-safe order
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Routes', 'U') IS NOT NULL DROP TABLE dbo.Routes;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- Create Users table
CREATE TABLE dbo.Users (
    UserID          INT             IDENTITY(1,1)   PRIMARY KEY,
    Role            VARCHAR(20)     NOT NULL        CHECK (Role IN ('Organiser', 'Participant')),
    FirstName       VARCHAR(50)     NOT NULL,
    LastName        VARCHAR(50)     NOT NULL,
    Email           VARCHAR(100)    NOT NULL        UNIQUE,
    PasswordHash    VARCHAR(255)    NOT NULL,
    PhoneNumber     VARCHAR(20)     NULL,
    CreatedAt       DATETIME        NOT NULL        DEFAULT GETDATE()
);
GO

-- Create Events table
CREATE TABLE dbo.Events (
    EventID             INT             IDENTITY(1,1)   PRIMARY KEY,
    OrganiserID         INT             NOT NULL,
    EventName           VARCHAR(100)    NOT NULL,
    EventDate           DATE            NOT NULL,
    Location            VARCHAR(150)    NOT NULL,
    Description         VARCHAR(8000)   NULL,
    WeatherLocationCode VARCHAR(20)     NULL,
    CreatedAt           DATETIME        NOT NULL        DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserID) REFERENCES dbo.Users(UserID)
);
GO

-- Create Routes table
CREATE TABLE dbo.Routes (
    RouteID         INT             IDENTITY(1,1)   PRIMARY KEY,
    EventID         INT             NOT NULL,
    RouteName       VARCHAR(100)    NOT NULL,
    DistanceKM      DECIMAL(6,2)    NOT NULL,
    ElevationGainM  INT             NOT NULL        DEFAULT 0,
    MapURL          VARCHAR(255)    NULL,
    CONSTRAINT FK_Routes_Event FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID)
);
GO

-- Create Categories table
CREATE TABLE dbo.Categories (
    CategoryID       INT             IDENTITY(1,1)   PRIMARY KEY,
    EventID          INT             NOT NULL,
    CategoryName     VARCHAR(50)     NOT NULL,
    DistanceKM       DECIMAL(6,2)    NOT NULL,
    MaxParticipants  INT             NOT NULL        DEFAULT 100,
    EntryFee         DECIMAL(8,2)    NOT NULL        DEFAULT 0,
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID)
);
GO

-- Create Enrolments table
CREATE TABLE dbo.Enrolments (
    EnrolmentID     INT             IDENTITY(1,1)   PRIMARY KEY,
    ParticipantID   INT             NOT NULL,
    CategoryID      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL        DEFAULT GETDATE(),
    BibNumber       VARCHAR(10)     NULL,
    Status          VARCHAR(20)     NOT NULL        DEFAULT 'Confirmed' CHECK (Status IN ('Confirmed', 'Cancelled', 'Pending')),
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Category FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantID, CategoryID)
);
GO

-- Create Results table
CREATE TABLE dbo.Results (
    ResultID        INT             IDENTITY(1,1)   PRIMARY KEY,
    EnrolmentID     INT             NOT NULL        UNIQUE,
    FinishTime      TIME            NULL,
    Position        INT             NULL,
    Status          VARCHAR(20)     NOT NULL        DEFAULT 'Finished' CHECK (Status IN ('Finished', 'DNF', 'DSQ')),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentID) REFERENCES dbo.Enrolments(EnrolmentID)
);
GO

-- Insert sample data into Users table
INSERT INTO dbo.Users (Role, FirstName, LastName, Email, PasswordHash, PhoneNumber) VALUES
('Organiser',   'Naledi',   'Khumalo', 'naledi.khumalo@raceday.co.za', 'hashed_pw_1', '0821234567'),
('Organiser',   'Pieter',   'van Wyk', 'pieter.vanwyk@raceday.co.za',  'hashed_pw_2', '0827654321'),
('Participant', 'Thabo',    'Mokoena', 'thabo.mokoena@example.com',    'hashed_pw_3', '0731122334'),
('Participant', 'Emma',     'Botha',   'emma.botha@example.com',       'hashed_pw_4', '0739988776');
GO

-- Insert sample data into Events table
INSERT INTO dbo.Events (OrganiserID, EventName, EventDate, Location, Description, WeatherLocationCode) VALUES
(1, 'Cape Town Cycle Tour',      '2026-03-08', 'Cape Town, Western Cape', 'Iconic 109km cycling race around the Cape Peninsula.', 'ZA-CT'),
(1, 'Soweto Marathon',           '2026-11-01', 'Soweto, Gauteng',         'Community road running event through historic Soweto.', 'ZA-SOW'),
(2, 'Two Oceans Half Marathon',  '2026-04-04', 'Cape Town, Western Cape', 'Scenic half marathon along the Cape Peninsula coastline.', 'ZA-CT');
GO

-- Insert sample data into Routes table
INSERT INTO dbo.Routes (EventID, RouteName, DistanceKM, ElevationGainM, MapURL) VALUES
(1, 'Peninsula Loop',      109.00, 1200, 'https://maps.example.com/cape-town-cycle-tour'),
(2, 'Soweto Heritage Route', 42.20,  350, 'https://maps.example.com/soweto-marathon'),
(3, 'Ocean View Course',     21.10,  400, 'https://maps.example.com/two-oceans-half');
GO

-- Insert sample data into Categories table
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKM, MaxParticipants, EntryFee) VALUES
(1, 'Individual Race',   109.00, 15000, 550.00),
(1, 'Mini Peloton (35km)', 35.00,  5000, 300.00),
(2, 'Full Marathon',      42.20,  8000, 250.00),
(2, '10km Fun Run',       10.00,  3000, 120.00),
(3, 'Half Marathon',      21.10,  11000, 350.00);
GO

-- Insert sample data into Enrolments table
INSERT INTO dbo.Enrolments (ParticipantID, CategoryID, BibNumber, Status) VALUES
(3, 1, 'CT-10234', 'Confirmed'),
(3, 3, 'SM-05521', 'Confirmed'),
(4, 5, 'TO-08890', 'Confirmed'),
(4, 2, 'CT-10555', 'Confirmed');
GO

-- Insert sample data into Results table
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status) VALUES
(1, '04:32:10', 214, 'Finished'),
(4, '01:58:47', 89,  'Finished');
GO
