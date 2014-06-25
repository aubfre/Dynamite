﻿#
# Module 'Dynamite.PowerShell.Toolkit'
# Generated by: GSoft, Team Dynamite.
# Generated on: 03/05/2014
# > GSoft & Dynamite : http://www.gsoft.com
# > Dynamite Github : https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit
# > Documentation : https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit/wiki
# from http://blogs.technet.com/b/heyscriptingguy/archive/2010/09/14/use-powershell-to-script-changes-to-the-sharepoint-web-config-file.aspx

<#
	.SYNOPSIS
		Enables the BLOB cache on the specified Web Application

	.DESCRIPTION
		Enables the BLOB cache on the specified Web Application

    --------------------------------------------------------------------------------------
    Module 'Dynamite.PowerShell.Toolkit'
    by: GSoft, Team Dynamite.
    > GSoft & Dynamite : http://www.gsoft.com
    > Dynamite Github : https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit
    > Documentation : https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit/wiki
    --------------------------------------------------------------------------------------
    
	.PARAMETER  WebApplication
		The Web Application object on which to enable the BLOB cache.

	.PARAMETER  Location
		The directory where the cached files will be stored.

	.PARAMETER  MaxAge
		Specifies the maximum amount of time in seconds that the client browser caches BLOBs downloaded to the client computer. 
        If the downloaded items have not expired since the last download, the same items are not re-requested when the page is requested. 
        The max-age attribute is set by default to 86400 seconds (that is, 24 hours), but it can be set to a time period of 0 or greater.

	.PARAMETER  MaxSize
		The maximum allowable size of the disk-based cache in gigabytes.

	.EXAMPLE
		PS C:\> Enable-DSPBlobCache -WebApplication "http://sp2013"

	.INPUTS
		System.String or SPWebApplication, System.String, System.Int, System.Int

	.OUTPUTS
		No output (Logging when -Verbose)
        
  .LINK
    GSoft, Team Dynamite on Github
    > https://github.com/GSoft-SharePoint
    
    Dynamite PowerShell Toolkit on Github
    > https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit
    
    Documentation
    > https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit/wiki
    
#>
function Enable-DSPBlobCache {

	[CmdletBinding()]
    param(

        [Parameter(Mandatory=$true, Position=0)]
        [Microsoft.SharePoint.PowerShell.SPWebApplicationPipeBind]$WebApplication,
        [Parameter(Mandatory=$false, Position=1)]
        [string]$Location,
        [Parameter(Mandatory=$false, Position=2)]
        [int]$MaxAge,
        [Parameter(Mandatory=$false, Position=3)]
        [int]$MaxSize
    )
	
	$WebApp = $WebApplication.Read()

	# SPWebConfigModification to enable BlobCache
    Write-Host "Enabling the BLOB cache on '$($WebApp.Url)'"
	$configMod1 = New-Object Microsoft.SharePoint.Administration.SPWebConfigModification
	$configMod1.Path = "configuration/SharePoint/BlobCache" 
	$configMod1.Name = "enabled" 
	$configMod1.Sequence = 0
	$configMod1.Owner = "BlobCacheMod" 
	## SPWebConfigModificationType.EnsureChildNode -> 0
	## SPWebConfigModificationType.EnsureAttribute -> 1
	## SPWebConfigModificationType.EnsureSection -> 2
	$configMod1.Type = 1
	$configMod1.Value = "true" 
	$WebApp.WebConfigModifications.Add($configMod1)
	
	# SPWebConfigModification to enable client-side Blob caching (max-age)
    if($MaxAge -gt 0)
    {

        Write-Verbose "Configuring the 'max-age' to '$MaxAge'"
	    $configMod2 = New-Object Microsoft.SharePoint.Administration.SPWebConfigModification
	    $configMod2.Path = "configuration/SharePoint/BlobCache" 
	    $configMod2.Name = "max-age" 
	    $configMod2.Sequence = 0
	    $configMod2.Owner = "BlobCacheMod" 
	
	    ## SPWebConfigModificationType.EnsureChildNode -> 0
	    ## SPWebConfigModificationType.EnsureAttribute -> 1
	    ## SPWebConfigModificationType.EnsureSection -> 2
	
	    $configMod2.Type = 1
	    $configMod2.Value = $MaxAge.ToString() 
	    $WebApp.WebConfigModifications.Add($configMod2)
    }
	
	# SPWebConfigurationModification to move blobstore location
    if(-not [string]::IsNullOrEmpty($Location))
    {		
        Write-Verbose "Configuring the 'location' to '$Location'"
	    $configMod3 = New-Object Microsoft.SharePoint.Administration.SPWebConfigModification
	    $configMod3.Path = "configuration/SharePoint/BlobCache" 		
	    $configMod3.Name = "location"
	    $configMod3.Sequence = 0
	    $configMod3.Owner = "BlobCacheMod" 
	    $configMod3.Type = 1
	    $configMod3.Value = $Location
	    $WebApp.WebConfigModifications.Add($configMod3)
    }	
    
	# SPWebConfigurationModification to configure max-size (in GB)
    if($MaxSize -gt 0)
    {
        Write-Verbose "Configuring the 'maxSize' to '$MaxSize'"
	    $configMod4 = New-Object Microsoft.SharePoint.Administration.SPWebConfigModification
	    $configMod4.Path = "configuration/SharePoint/BlobCache" 
	    $configMod4.Name = "maxSize" 
	    $configMod4.Sequence = 0
	    $configMod4.Owner = "BlobCacheMod" 
	
	    ## SPWebConfigModificationType.EnsureChildNode -> 0
	    ## SPWebConfigModificationType.EnsureAttribute -> 1
	    ## SPWebConfigModificationType.EnsureSection -> 2
	
	    $configMod4.Type = 1
	    $configMod4.Value = $MaxSize.ToString() 
	    $WebApp.WebConfigModifications.Add($configMod4)
    }	
		
		
	
	# Update, and apply
	$WebApp.Update()
	
	# Added a 5 second sleep period for multiple server farms
	Write-Host "Waiting for web config modifications to propagate..."
	Start-Sleep -s 5
	$WebApp.WebService.ApplyWebConfigModifications()
} 

<#
	.SYNOPSIS
		Disables the BLOB cache on the specified Web Application

	.DESCRIPTION
		Disables the BLOB cache on the specified Web Application

    --------------------------------------------------------------------------------------
    Module 'Dynamite.PowerShell.Toolkit'
    by: GSoft, Team Dynamite.
    > GSoft & Dynamite : http://www.gsoft.com
    > Dynamite Github : https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit
    > Documentation : https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit/wiki
    --------------------------------------------------------------------------------------
    
	.PARAMETER  WebApplication
		The Web Application object on which to enable the BLOB cache.

	.EXAMPLE
		PS C:\> Disable-DSPBlobCache -WebApplication "http://sp2013"

	.INPUTS
		System.String or SPWebApplication

	.OUTPUTS
		No output (Logging when -Verbose)
        
  .LINK
    GSoft, Team Dynamite on Github
    > https://github.com/GSoft-SharePoint
    
    Dynamite PowerShell Toolkit on Github
    > https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit
    
    Documentation
    > https://github.com/GSoft-SharePoint/Dynamite-PowerShell-Toolkit/wiki
    
#>
function Disable-DSPBlobCache {
	[CmdletBinding()]
	param(
        [Parameter(Mandatory=$true, Position=0)]
        [Microsoft.SharePoint.PowerShell.SPWebApplicationPipeBind]$WebApplication
	)
	
	$WebApp = $WebApplication.Read()

    Write-Host "Disabling the BLOB cache on '$($WebApp.Url)'"

	$mods = @()
	foreach ($mod in $WebApp.WebConfigModifications) 
    {
		if ($mod.Owner -eq "BlobCacheMod") 
        {
			$mods += $mod
		}
    }
		
	foreach ($mod in $mods) 
    {
        Write-Verbose "Removing web config modification '$($mod.Name)' on path '$($mod.Path)'"
		[void] $WebApp.WebConfigModifications.Remove($mod)
	}
		
	$WebApp.Update()
	
	# Added a 5 second sleep period for multiple server farms
	Write-Host "Waiting for web config modifications to propagate..."
	Start-Sleep -s 5
	$WebApp.WebService.ApplyWebConfigModifications()
} 