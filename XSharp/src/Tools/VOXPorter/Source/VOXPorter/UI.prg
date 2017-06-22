USING System.IO
USING System.Reflection
USING System.Windows.Forms

CLASS xPorterUI INHERIT System.Windows.Forms.Form IMPLEMENTS IProgressBar

	PROTECT oExitButton AS System.Windows.Forms.Button
	PROTECT oGroupBox3 AS System.Windows.Forms.GroupBox
	PROTECT oLabelProgress AS System.Windows.Forms.Label
	PROTECT oProgressBar AS System.Windows.Forms.ProgressBar
	PROTECT oxPortButton AS System.Windows.Forms.Button
	PROTECT oGroupBox2 AS System.Windows.Forms.GroupBox
	PROTECT oTextAppName AS System.Windows.Forms.TextBox
	PROTECT oLabelSource2 AS System.Windows.Forms.Label
	PROTECT oTextSolutionName AS System.Windows.Forms.TextBox
	PROTECT oLabelSource1 AS System.Windows.Forms.Label
	PROTECT oButtonOutput AS System.Windows.Forms.Button
	PROTECT oButtonSource AS System.Windows.Forms.Button
	PROTECT oRadioSDKDefines AS System.Windows.Forms.RadioButton
	PROTECT oTextOutput AS System.Windows.Forms.TextBox
	PROTECT oLabel2 AS System.Windows.Forms.Label
	PROTECT oLabelSource AS System.Windows.Forms.Label
	PROTECT oTextSource AS System.Windows.Forms.TextBox
	PROTECT oRadioSDKFromFolder AS System.Windows.Forms.RadioButton
	PROTECT oRadioFromFolder AS System.Windows.Forms.RadioButton
	PROTECT oRadioFromAefsInFolder AS System.Windows.Forms.RadioButton
	PROTECT oRadioFromAef AS System.Windows.Forms.RadioButton
	PROTECT oGroupBox1 AS System.Windows.Forms.GroupBox
	PROTECT oCheckNotOverwriteProjectFiles AS System.Windows.Forms.CheckBox
	PROTECT oOptionsList AS System.Windows.Forms.CheckedListBox
	// User code starts here (DO NOT remove this line)  ##USER##
	
	PROTECT lExporting AS LOGIC
	CONSTRUCTOR(oOptions AS xPorterOptions)

		SUPER()

		SELF:InitializeForm()
		
		LOCAL oType AS Type
		oType := TypeOf(xPorterOptions)
		LOCAL aFields AS FieldInfo[]
		aFields := oType:GetFields()
		FOREACH oField AS FieldInfo IN aFields
			SELF:oOptionsList:Items:Add(oField:Name , (LOGIC)oField:GetValue(oOptions))
		NEXT
		
		SELF:oTextOutput:Text := DefaultOutputFolder
		SELF:oTextSource:Text := DefaultSourceFolder

	RETURN
	PROTECTED METHOD InitializeForm() AS VOID
	
	// IDE generated code (please DO NOT modify)
	
		LOCAL oResourceManager AS System.Resources.ResourceManager

		oResourceManager := System.Resources.ResourceManager{ "Designers" , System.Reflection.Assembly.GetExecutingAssembly() }

		SELF:oExitButton := System.Windows.Forms.Button{}
		SELF:oGroupBox3 := System.Windows.Forms.GroupBox{}
		SELF:oLabelProgress := System.Windows.Forms.Label{}
		SELF:oProgressBar := System.Windows.Forms.ProgressBar{}
		SELF:oxPortButton := System.Windows.Forms.Button{}
		SELF:oGroupBox2 := System.Windows.Forms.GroupBox{}
		SELF:oTextAppName := System.Windows.Forms.TextBox{}
		SELF:oLabelSource2 := System.Windows.Forms.Label{}
		SELF:oTextSolutionName := System.Windows.Forms.TextBox{}
		SELF:oLabelSource1 := System.Windows.Forms.Label{}
		SELF:oButtonOutput := System.Windows.Forms.Button{}
		SELF:oButtonSource := System.Windows.Forms.Button{}
		SELF:oRadioSDKDefines := System.Windows.Forms.RadioButton{}
		SELF:oTextOutput := System.Windows.Forms.TextBox{}
		SELF:oLabel2 := System.Windows.Forms.Label{}
		SELF:oLabelSource := System.Windows.Forms.Label{}
		SELF:oTextSource := System.Windows.Forms.TextBox{}
		SELF:oRadioSDKFromFolder := System.Windows.Forms.RadioButton{}
		SELF:oRadioFromFolder := System.Windows.Forms.RadioButton{}
		SELF:oRadioFromAefsInFolder := System.Windows.Forms.RadioButton{}
		SELF:oRadioFromAef := System.Windows.Forms.RadioButton{}
		SELF:oGroupBox1 := System.Windows.Forms.GroupBox{}
		SELF:oCheckNotOverwriteProjectFiles := System.Windows.Forms.CheckBox{}
		SELF:oOptionsList := System.Windows.Forms.CheckedListBox{}

		SELF:SuspendLayout()

		SELF:ClientSize := System.Drawing.Size{604 , 414}
		SELF:FormBorderStyle := System.Windows.Forms.FormBorderStyle.FixedSingle
		SELF:Icon := (System.Drawing.Icon)oResourceManager:GetObject( "XSharpSm.ico" )
		SELF:Location := System.Drawing.Point{100 , 100}
		SELF:MaximizeBox := FALSE
		SELF:MinimizeBox := FALSE
		SELF:Name := "xPorterUI"
		SELF:Text := "VO-xPorter beta 0.12"

		SELF:CancelButton := SELF:oExitButton
		SELF:oExitButton:Click += SELF:ExitButtonClick
		SELF:oExitButton:Location := System.Drawing.Point{474 , 385}
		SELF:oExitButton:Name := "ExitButton"
		SELF:oExitButton:Size := System.Drawing.Size{119 , 23}
		SELF:oExitButton:TabIndex := 4
		SELF:oExitButton:Text := "Exit"
		SELF:Controls:Add(SELF:oExitButton)
		
		SELF:oGroupBox3:SuspendLayout()
		SELF:oGroupBox3:Location := System.Drawing.Point{11 , 297}
		SELF:oGroupBox3:Name := "GroupBox3"
		SELF:oGroupBox3:Size := System.Drawing.Size{582 , 79}
		SELF:oGroupBox3:TabIndex := 3
		SELF:oGroupBox3:Text := "Progress"
		SELF:Controls:Add(SELF:oGroupBox3)
		

		SELF:oLabelProgress:AutoSize := TRUE
		SELF:oLabelProgress:Location := System.Drawing.Point{18 , 17}
		SELF:oLabelProgress:Name := "LabelProgress"
		SELF:oLabelProgress:Size := System.Drawing.Size{102 , 17}
		SELF:oLabelProgress:TabIndex := 9
		SELF:oLabelProgress:Text := "xPorting progress..."
		SELF:oLabelProgress:TextAlign := System.Drawing.ContentAlignment.BottomLeft
		SELF:oGroupBox3:Controls:Add(SELF:oLabelProgress)
		
		SELF:oProgressBar:Location := System.Drawing.Point{18 , 37}
		SELF:oProgressBar:Name := "ProgressBar"
		SELF:oProgressBar:Size := System.Drawing.Size{546 , 26}
		SELF:oProgressBar:TabIndex := 0
		SELF:oGroupBox3:Controls:Add(SELF:oProgressBar)
		
		SELF:oxPortButton:Click += SELF:xPortButton_Click
		SELF:oxPortButton:Location := System.Drawing.Point{338 , 385}
		SELF:oxPortButton:Name := "xPortButton"
		SELF:oxPortButton:Size := System.Drawing.Size{119 , 23}
		SELF:oxPortButton:TabIndex := 2
		SELF:oxPortButton:Text := "xPort!"
		SELF:Controls:Add(SELF:oxPortButton)
		
		SELF:oGroupBox2:SuspendLayout()
		SELF:oGroupBox2:Location := System.Drawing.Point{11 , 9}
		SELF:oGroupBox2:Name := "GroupBox2"
		SELF:oGroupBox2:Size := System.Drawing.Size{316 , 284}
		SELF:oGroupBox2:TabIndex := 0
		SELF:oGroupBox2:Text := "xPort"
		SELF:Controls:Add(SELF:oGroupBox2)
		

		SELF:oTextAppName:Location := System.Drawing.Point{171 , 256}
		SELF:oTextAppName:Name := "TextAppName"
		SELF:oTextAppName:Size := System.Drawing.Size{137 , 20}
		SELF:oTextAppName:TabIndex := 14
		SELF:oGroupBox2:Controls:Add(SELF:oTextAppName)
		
		SELF:oLabelSource2:AutoSize := TRUE
		SELF:oLabelSource2:Location := System.Drawing.Point{171 , 238}
		SELF:oLabelSource2:Name := "LabelSource2"
		SELF:oLabelSource2:Size := System.Drawing.Size{99 , 17}
		SELF:oLabelSource2:TabIndex := 13
		SELF:oLabelSource2:Text := "App / library name:"
		SELF:oGroupBox2:Controls:Add(SELF:oLabelSource2)
		
		SELF:oTextSolutionName:Location := System.Drawing.Point{18 , 256}
		SELF:oTextSolutionName:Name := "TextSolutionName"
		SELF:oTextSolutionName:Size := System.Drawing.Size{137 , 20}
		SELF:oTextSolutionName:TabIndex := 12
		SELF:oGroupBox2:Controls:Add(SELF:oTextSolutionName)
		
		SELF:oLabelSource1:AutoSize := TRUE
		SELF:oLabelSource1:Location := System.Drawing.Point{18 , 238}
		SELF:oLabelSource1:Name := "LabelSource1"
		SELF:oLabelSource1:Size := System.Drawing.Size{80 , 17}
		SELF:oLabelSource1:TabIndex := 11
		SELF:oLabelSource1:Text := "Solution name:"
		SELF:oGroupBox2:Controls:Add(SELF:oLabelSource1)
		
		SELF:oButtonOutput:Click += SELF:ButtonOutput_Click
		SELF:oButtonOutput:Location := System.Drawing.Point{279 , 213}
		SELF:oButtonOutput:Name := "ButtonOutput"
		SELF:oButtonOutput:Size := System.Drawing.Size{27 , 20}
		SELF:oButtonOutput:TabIndex := 10
		SELF:oButtonOutput:Text := "..."
		SELF:oGroupBox2:Controls:Add(SELF:oButtonOutput)
		
		SELF:oButtonSource:Click += SELF:ButtonSource_Click
		SELF:oButtonSource:Location := System.Drawing.Point{279 , 171}
		SELF:oButtonSource:Name := "ButtonSource"
		SELF:oButtonSource:Size := System.Drawing.Size{27 , 20}
		SELF:oButtonSource:TabIndex := 7
		SELF:oButtonSource:Text := "..."
		SELF:oGroupBox2:Controls:Add(SELF:oButtonSource)
		
		SELF:oRadioSDKDefines:AutoSize := TRUE
		SELF:oRadioSDKDefines:Click += SELF:Radio_Click
		SELF:oRadioSDKDefines:Enabled := FALSE
		SELF:oRadioSDKDefines:Location := System.Drawing.Point{18 , 117}
		SELF:oRadioSDKDefines:Name := "RadioSDKDefines"
		SELF:oRadioSDKDefines:Size := System.Drawing.Size{228 , 18}
		SELF:oRadioSDKDefines:TabIndex := 4
		SELF:oRadioSDKDefines:Text := "SDK defines only library from Volib folder"
		SELF:oGroupBox2:Controls:Add(SELF:oRadioSDKDefines)
		
		SELF:oTextOutput:Location := System.Drawing.Point{18 , 213}
		SELF:oTextOutput:Name := "TextOutput"
		SELF:oTextOutput:Size := System.Drawing.Size{254 , 20}
		SELF:oTextOutput:TabIndex := 9
		SELF:oGroupBox2:Controls:Add(SELF:oTextOutput)
		
		SELF:oLabel2:AutoSize := TRUE
		SELF:oLabel2:Location := System.Drawing.Point{18 , 195}
		SELF:oLabel2:Name := "Label2"
		SELF:oLabel2:Size := System.Drawing.Size{73 , 17}
		SELF:oLabel2:TabIndex := 8
		SELF:oLabel2:Text := "Output folder:"
		SELF:oGroupBox2:Controls:Add(SELF:oLabel2)
		
		SELF:oLabelSource:AutoSize := TRUE
		SELF:oLabelSource:Location := System.Drawing.Point{18 , 153}
		SELF:oLabelSource:Name := "LabelSource"
		SELF:oLabelSource:Size := System.Drawing.Size{75 , 17}
		SELF:oLabelSource:TabIndex := 5
		SELF:oLabelSource:Text := "Source folder:"
		SELF:oGroupBox2:Controls:Add(SELF:oLabelSource)
		
		SELF:oTextSource:Location := System.Drawing.Point{18 , 171}
		SELF:oTextSource:Name := "TextSource"
		SELF:oTextSource:Size := System.Drawing.Size{254 , 20}
		SELF:oTextSource:TabIndex := 6
		SELF:oTextSource:TextChanged += SELF:TextSource_TextChanged
		SELF:oGroupBox2:Controls:Add(SELF:oTextSource)
		
		SELF:oRadioSDKFromFolder:AutoSize := TRUE
		SELF:oRadioSDKFromFolder:Click += SELF:Radio_Click
		SELF:oRadioSDKFromFolder:Enabled := FALSE
		SELF:oRadioSDKFromFolder:Location := System.Drawing.Point{18 , 93}
		SELF:oRadioSDKFromFolder:Name := "RadioSDKFromFolder"
		SELF:oRadioSDKFromFolder:Size := System.Drawing.Size{174 , 18}
		SELF:oRadioSDKFromFolder:TabIndex := 3
		SELF:oRadioSDKFromFolder:Text := "SDK libraries from Volib folder"
		SELF:oGroupBox2:Controls:Add(SELF:oRadioSDKFromFolder)
		
		SELF:oRadioFromFolder:AutoSize := TRUE
		SELF:oRadioFromFolder:Checked := FALSE
		SELF:oRadioFromFolder:Click += SELF:Radio_Click
		SELF:oRadioFromFolder:Location := System.Drawing.Point{18 , 69}
		SELF:oRadioFromFolder:Name := "RadioFromFolder"
		SELF:oRadioFromFolder:Size := System.Drawing.Size{170 , 18}
		SELF:oRadioFromFolder:TabIndex := 2
		SELF:oRadioFromFolder:Text := "Application from files in folder"
		SELF:oGroupBox2:Controls:Add(SELF:oRadioFromFolder)
		
		SELF:oRadioFromAefsInFolder:AutoSize := TRUE
		SELF:oRadioFromAefsInFolder:Click += SELF:Radio_Click
		SELF:oRadioFromAefsInFolder:Location := System.Drawing.Point{18 , 45}
		SELF:oRadioFromAefsInFolder:Name := "RadioFromAefsInFolder"
		SELF:oRadioFromAefsInFolder:Size := System.Drawing.Size{183 , 18}
		SELF:oRadioFromAefsInFolder:TabIndex := 1
		SELF:oRadioFromAefsInFolder:Text := "Applications from AEFs in folder"
		SELF:oGroupBox2:Controls:Add(SELF:oRadioFromAefsInFolder)
		
		SELF:oRadioFromAef:AutoSize := TRUE
		SELF:oRadioFromAef:Checked := TRUE
		SELF:oRadioFromAef:Click += SELF:Radio_Click
		SELF:oRadioFromAef:Enabled := TRUE
		SELF:oRadioFromAef:Location := System.Drawing.Point{18 , 21}
		SELF:oRadioFromAef:Name := "RadioFromAef"
		SELF:oRadioFromAef:Size := System.Drawing.Size{146 , 18}
		SELF:oRadioFromAef:TabIndex := 0
		SELF:oRadioFromAef:Text := "Application from AEF file"
		SELF:oGroupBox2:Controls:Add(SELF:oRadioFromAef)
		
		SELF:oGroupBox1:SuspendLayout()
		SELF:oGroupBox1:Location := System.Drawing.Point{338 , 9}
		SELF:oGroupBox1:Name := "GroupBox1"
		SELF:oGroupBox1:Size := System.Drawing.Size{255 , 284}
		SELF:oGroupBox1:TabIndex := 1
		SELF:oGroupBox1:Text := "xPort options"
		SELF:Controls:Add(SELF:oGroupBox1)
		

		SELF:oCheckNotOverwriteProjectFiles:Location := System.Drawing.Point{15 , 243}
		SELF:oCheckNotOverwriteProjectFiles:Name := "CheckNotOverwriteProjectFiles"
		SELF:oCheckNotOverwriteProjectFiles:Size := System.Drawing.Size{222 , 30}
		SELF:oCheckNotOverwriteProjectFiles:TabIndex := 1
		SELF:oCheckNotOverwriteProjectFiles:Text := "Do not overwrite project files if they already exist"
		SELF:oGroupBox1:Controls:Add(SELF:oCheckNotOverwriteProjectFiles)
		
		SELF:oOptionsList:IntegralHeight := FALSE
		SELF:oOptionsList:Location := System.Drawing.Point{15 , 26}
		SELF:oOptionsList:Name := "OptionsList"
		SELF:oOptionsList:Size := System.Drawing.Size{222 , 207}
		SELF:oOptionsList:TabIndex := 0
		SELF:oGroupBox1:Controls:Add(SELF:oOptionsList)
		
		SELF:oGroupBox1:ResumeLayout()
		SELF:oGroupBox2:ResumeLayout()
		SELF:oGroupBox3:ResumeLayout()
		SELF:ResumeLayout()

	RETURN

	PROTECTED METHOD EnableDisableControls() AS VOID
		LOCAL cSolutionName := "", cAppName := "" AS STRING
		SELF:oTextAppName:Enabled := SELF:oRadioFromAef:Checked .or. SELF:oRadioFromFolder:Checked .or. SELF:oRadioSDKDefines:Checked
		TRY
			DO CASE
			CASE SELF:oRadioFromAef:Checked
//				cAppName := FileInfo{SELF:oTextSource:Text}:Name
				cAppName := GetFilenameNoExt(SELF:oTextSource:Text)
				cSolutionName := cAppName
			CASE SELF:oRadioFromAefsInFolder:Checked
				cAppName := ""
				cSolutionName := DirectoryInfo{SELF:oTextSource:Text}:Name
			CASE SELF:oRadioFromFolder:Checked
				cAppName := DirectoryInfo{SELF:oTextSource:Text}:Name
				cSolutionName := DirectoryInfo{SELF:oTextSource:Text}:Name
			CASE SELF:oRadioSDKFromFolder:Checked
				cAppName := ""
				cSolutionName := "VOSDK"
			CASE SELF:oRadioSDKDefines:Checked
				cAppName := "SDK_Defines"
				cSolutionName := "SDK_Defines"
			END CASE
		CATCH e AS Exception
			cSolutionName := "SolutionName"
			cAppName := "AppName"
		END TRY
		SELF:oTextSolutionName:Text := cSolutionName
		SELF:oTextAppName:Text := cAppName
	RETURN

	PROTECTED METHOD Radio_Click(sender AS System.Object , e AS System.EventArgs) AS VOID
		IF sender == SELF:oRadioFromAef
			SELF:oLabelSource:Text := "Source filename:"
		ELSE
			SELF:oLabelSource:Text := "Source folder:"
		END IF
		SELF:EnableDisableControls()
	RETURN

	PROTECTED METHOD TextSource_TextChanged(sender AS System.Object , e AS System.EventArgs) AS VOID
		SELF:EnableDisableControls()
	RETURN

	PROTECTED METHOD ButtonSource_Click(sender AS System.Object , e AS System.EventArgs) AS VOID
		IF SELF:oRadioFromAef:Checked
			LOCAL oDlg AS OpenFileDialog
			oDlg := OpenFileDialog{}
			oDlg:Filter := "VO AEF files (*.aef)|*.aef|All files (*.*)|*.*"
			IF oDlg:ShowDialog() == DialogResult.OK
				SELF:oTextSource:Text := oDlg:FileName
			ENDIF
		ELSE
			LOCAL oDlg AS FolderBrowserDialog
			oDlg := FolderBrowserDialog{}
			IF .not. String.IsNullOrWhiteSpace(SELF:oTextSource:Text)
				oDlg:SelectedPath := SELF:oTextSource:Text
			END IF
			IF oDlg:ShowDialog() == DialogResult.OK
				SELF:oTextSource:Text := oDlg:SelectedPath
			END IF
		END IF
	RETURN

	PROTECTED METHOD ButtonOutput_Click(sender AS System.Object , e AS System.EventArgs) AS VOID
		LOCAL oDlg AS FolderBrowserDialog
		oDlg := FolderBrowserDialog{}
		IF .not. String.IsNullOrWhiteSpace(SELF:oTextOutput:Text)
			oDlg:SelectedPath := SELF:oTextOutput:Text
		END IF
		IF oDlg:ShowDialog() == DialogResult.OK
			SELF:oTextOutput:Text := oDlg:SelectedPath
		END IF
	RETURN

	METHOD SetProgressBarRange(nValue AS INT) AS VOID
		SELF:oProgressBar:Value := 0
		SELF:oProgressBar:Minimum := 0
		SELF:oProgressBar:Maximum := nValue
	RETURN
	METHOD SetProgressBarValue(nValue AS INT) AS VOID
		SELF:oProgressBar:Value := nValue
	RETURN
	METHOD SetProgressText(cText AS STRING) AS VOID
		SELF:oLabelProgress:Text := cText
		Application.DoEvents()
	RETURN
	METHOD AdvanceProgressbar() AS VOID
		IF SELF:oProgressBar:Value < SELF:oProgressBar:Maximum
			SELF:oProgressBar:Value ++
			Application.DoEvents()
		END IF
	RETURN

	PROTECTED METHOD ExitButtonClick(sender AS System.Object , e AS System.EventArgs) AS VOID
		SELF:Close()
	RETURN

	PROTECTED METHOD OnClosing(e AS System.ComponentModel.CancelEventArgs) AS VOID
		SUPER:OnClosing(e)
		IF SELF:lExporting
			e:Cancel := TRUE
		END IF
	RETURN

	PROTECTED METHOD xPortButton_Click(sender AS System.Object , e AS System.EventArgs) AS VOID
		LOCAL cSourceFolder , cOutputFolder AS STRING
		LOCAL cAppName , cSolutionName AS STRING

		cSourceFolder := SELF:oTextSource:Text:Trim()
		cOutputFolder := SELF:oTextOutput:Text:Trim()
		
		cSolutionName := SELF:oTextSolutionName:Text:Trim()
		cAppName      := SELF:oTextAppName:Text:Trim()
		
		IF SELF:oRadioSDKFromFolder:Checked .and. SELF:oTextSource:Text == ""
			IF Directory.Exists("C:\cavo28sp4\Volib")
				SELF:oTextSource:Text := "C:\cavo28sp4\Volib"
				Application.DoEvents()
			END IF
		END IF
		
		IF SELF:oRadioFromAef:Checked
			IF .not. File.Exists(cSourceFolder)
				MessageBox.Show("Source aef file does not exist" , "xPorter")
				RETURN
			END IF
		ELSE
			IF .not. Directory.Exists(cSourceFolder)
				MessageBox.Show("Source folder does not exist" , "xPorter")
				RETURN
			END IF
			IF cSourceFolder:EndsWith("\")
				cSourceFolder := cSourceFolder:Substring(0 , cSourceFolder:Length - 1)
			END IF
		END IF
		IF SELF:oRadioSDKFromFolder:Checked .and. .not. cSourceFolder:ToUpper():EndsWith("VOLIB")
			MessageBox.Show("You must select a \VOLIB folder as source for xporting SDK files" , "xPorter")
			RETURN
		END IF
		IF SELF:oRadioFromFolder:Checked .and. Directory.GetFiles(cSourceFolder , "*.prg"):Length == 0
			MessageBox.Show("For xporting an app from files in a folder, you must specify a source folder taht contains .prg files." , "xPorter")
			RETURN
		END IF

/*		IF .not. Directory.Exists(cOutputFolder)
			MessageBox.Show("Output folder does not exist" , "xPorter")
		END IF*/
		Directory.CreateDirectory(cOutputFolder)

		LOCAL oType AS Type
		oType := TypeOf(xPorterOptions)
		LOCAL oOptions AS xPorterOptions
		LOCAL oObject AS OBJECT
		oOptions := xPorterOptions{}
		oObject := oOptions // needs boxing
		
		FOREACH oItem AS OBJECT IN SELF:oOptionsList:CheckedItems
			LOCAL oField AS FieldInfo
			oField := oType:GetField(oItem:ToString())
			oField:SetValue(oObject , TRUE)
		NEXT

		oOptions := (xPorterOptions)oObject
		xPorter.Options := oOptions
		
		SELF:oxPortButton:Enabled := FALSE
		SELF:lExporting := TRUE
		
		xPorter.OverWriteProjectFiles := .not. SELF:oCheckNotOverwriteProjectFiles:Checked
		
		DO CASE
		CASE SELF:oRadioFromAef:Checked
			xPorter.xPort_AppFromAef(cSourceFolder , cOutputFolder , cSolutionName , cAppName)
		CASE SELF:oRadioFromAefsInFolder:Checked
			xPorter.xPort_AppsFromAefsInFolder(cSourceFolder , cOutputFolder , cSolutionName)
		CASE SELF:oRadioFromFolder:Checked
			xPorter.xPort_AppFromFolder(cSourceFolder , cOutputFolder , cSolutionName , cAppName)
		CASE SELF:oRadioSDKFromFolder:Checked
			xPorter.xPort_VOSDK(cSourceFolder , cOutputFolder, cSolutionName)
		CASE SELF:oRadioSDKDefines:Checked
			xPorter.xPort_SDK_Defines(cSourceFolder , cOutputFolder , cSolutionName , cAppName)
		END CASE

		MessageBox.Show("xPorted to folder " + cOutputFolder , "xPort finished!")
		
		SELF:oxPortButton:Enabled := TRUE
		SELF:lExporting := FALSE
	RETURN

END CLASS
