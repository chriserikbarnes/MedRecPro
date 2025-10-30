<?xml version="1.0" encoding="utf-8"?>
<!--
The contents of this file are subject to the Health Level-7 Public
License Version 1.0 (the "License"); you may not use this file
except in compliance with the License. You may obtain a copy of the
License at http://www.hl7.org/HPL/hpl.txt.

Software distributed under the License is distributed on an "AS IS"
basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
the License for the specific language governing rights and
limitations under the License.

The Original Code is all this file.

The Initial Developer of the Original Code is Gunther Schadow.
Portions created by Initial Developer are Copyright (C) 2002-2004
Health Level Seven, Inc. All Rights Reserved.

Contributor(s): Steven Gitterman, Brian Keller

Revision: $Id: spl.xsl,v 1.52 2005/08/26 05:59:26 gschadow Exp $

Revision: $Id: spl-common.xsl,v 2.0 2006/08/18 04:11:00 sbsuggs Exp $

Revision: Path: MedRecPro/Views/Stylesheets/spl.xsl,v 1.3 2012/03/14 21:49:23 gschadow Exp $

-->
<xsl:transform xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
               xmlns:v3="urn:hl7-org:v3"
               version="1.0"
               exclude-result-prefixes="v3 xsl">

	<!-- Use relative import so it resolves under /api/stylesheets -->
	<xsl:import href="spl-common.xsl"/>

	<!-- Base resource directory, relative to this XSL’s own path -->
	<xsl:param name="resourcesdir">/api/stylesheets/</xsl:param>

	<!-- Data and rendering options -->
	<xsl:param name="show-subjects-xml" select="/.."/>
	<xsl:param name="show-data" select="1"/>

	<!-- CSS now relative -->
	<xsl:param name="css">/api/stylesheets/spl.css</xsl:param>

	<xsl:param name="show-section-numbers" select="/.."/>
	<xsl:param name="process-mixins" select="true()"/>
	<xsl:param name="core-base-url">http://www.accessdata.fda.gov/spl/core</xsl:param>
	<xsl:output method="html" version="1.0" encoding="UTF-8" indent="yes"/>
	<xsl:strip-space elements="*"/>
</xsl:transform>