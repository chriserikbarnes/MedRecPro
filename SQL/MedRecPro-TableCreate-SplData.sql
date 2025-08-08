USE [MedRecLocal]
GO

/****** Object:  Table [dbo].[SplData]    Script Date: 8/8/2025 12:57:28 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[SplData](
	[SplDataID] [bigint] IDENTITY(1,1) NOT NULL,
	[AspNetUsersID] [bigint] NULL,
	[SplDataGUID] [uniqueidentifier] NOT NULL,
	[SplXML] [nvarchar](max) NOT NULL,
	[Archive] [bit] NULL,
	[LogDate] [datetime] NULL,
 CONSTRAINT [PK_SplData] PRIMARY KEY CLUSTERED 
(
	[SplDataID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[SplData] ADD  CONSTRAINT [DF_SplData_Archive]  DEFAULT ((0)) FOR [Archive]
GO

ALTER TABLE [dbo].[SplData] ADD  CONSTRAINT [DF_SplData_LogDate]  DEFAULT (getutcdate()) FOR [LogDate]
GO

