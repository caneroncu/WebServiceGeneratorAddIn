﻿using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using VSLangProj;
using WSDLToWebService;
using System.Web.Services;
using System.Web.Services.Description;
using System.IO;
using WebServiceGeneratorAddIn.UI;
using System.Collections.Generic;

namespace WebServiceGeneratorAddIn
{
    /// <summary>The object for implementing an Add-in.</summary>
    /// <seealso class='IDTExtensibility2' />
    public class Connect : IDTExtensibility2, IDTCommandTarget
    {
        /// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
        public Connect()
        {
        }

        /// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
        /// <param term='application'>Root object of the host application.</param>
        /// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
        /// <param term='addInInst'>Object representing this Add-in.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            _applicationObject = (DTE2)application;
            _addInInstance = (AddIn)addInInst;

            //Tools tabına Add-In'i ekleme işlemleri
            #region ADD_IT_TO_TOOLS
            string addonDisplayName = "Web Service Generator";
            string toolsMenuName = "Tools";

            if (connectMode == ext_ConnectMode.ext_cm_UISetup)
            {
                object[] contextGUIDS = new object[] { };
                Commands2 commands = (Commands2)_applicationObject.Commands;

                //Place the command on the tools menu.
                //Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
                Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

                //Find the Tools command bar on the MenuBar command bar:
                CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
                CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

                //This try/catch block can be duplicated if you wish to add multiple commands to be handled by your Add-in,
                //  just make sure you also update the QueryStatus/Exec method to include the new command names.
                try
                {
                    //Add a command to the Commands collection:
                    Command command = commands.AddNamedCommand2(_addInInstance, "WebServiceGeneratorAddIn", addonDisplayName, "Executes the command for " + addonDisplayName, true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                    //Add a control for the command to the tools menu:
                    if ((command != null) && (toolsPopup != null))
                    {
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException)
                {
                    //If we are here, then the exception is probably because a command with that name
                    //  already exists. If so there is no need to recreate the command and we can 
                    //  safely ignore the exception.
                }
            }
            #endregion

            FrmProjectParams frmProjectParams = new FrmProjectParams(this);
            frmProjectParams.ShowDialog();
        }

        /// <summary>
        /// Verilen parametrelere göre otomatik olarak Web Service Solution'ı oluşturur.
        /// </summary>
        /// <param name="wsdlAddress"></param>
        /// <param name="solutionPath"></param>
        /// <param name="solutionName"></param>
        /// <param name="projectName"></param>
        /// <param name="templatePath"></param>
        public void GenerateWebService(string solutionPath, string solutionName, string projectName, string templatePath,
            string wsdlAddress = null, string content = null)
        {
            string projectPath = solutionPath + projectName;
            string interfaceName = null;
            string interfaceExtension = null;
            Project project = null;

            //Belirtilen path'te klasör varsa klasörü ve içini sil
            if (Directory.Exists(solutionPath))
            {
                Directory.Delete(solutionPath, true);
            }

            Solution solution = _applicationObject.Solution;
            solution.Create(solutionPath, solutionName);
            //Projenin eklenmesi - template baz alınarak
            solution.AddFromTemplate(templatePath, projectPath, projectName, false);
            //Eklenen projeyi çek
            project = solution.Projects.Item(1);
            solution.SaveAs(solutionName);

            ServiceDescription sd = null;

            if (wsdlAddress != null)
                sd = WebServiceGenerator.GetServiceDescriptionFromAddress(new Uri(wsdlAddress));
            else
                sd = WebServiceGenerator.GetServiceDescriptionFromParameter(content);

            //Interface adı I<ServisAdı> olarak çıkıyor, kaydederken extension'u da ekliyoruz
            interfaceName = string.Format("I{0}", sd.Bindings[0].Name);
            interfaceExtension = ".cs";

            string interfaceFullPath = string.Format(@"{0}\{1}{2}", projectPath, interfaceName, interfaceExtension);
            string currentNamespace = project.Name;
            WebServiceGenerator.Generate(sd, interfaceFullPath, ServiceDescriptionImportStyle.ServerInterface, currentNamespace);

            ProjectItems projectItems = project.ProjectItems;
            projectItems.AddFromFile(interfaceFullPath);

            implementInterface(project, interfaceName);

            string proxyFullPath = string.Format(@"{0}\{1}Proxy.cs", projectPath, sd.Bindings[0].Name);
            createProxy(project, sd, proxyFullPath);

            //Proje değişikliklerini kaydet
            project.Save();
        }

        private void createProxy(Project project, ServiceDescription serviceDescription, string proxyFullPath)
        {
            string currentNamespace = project.Name;
            WebServiceGenerator.Generate(serviceDescription, proxyFullPath, ServiceDescriptionImportStyle.Client, currentNamespace);
            project.ProjectItems.AddFromFile(proxyFullPath);
        }

        private void implementInterface(Project project, string interfaceName)
        {
            CodeElements elements = _applicationObject.ActiveDocument.ProjectItem.FileCodeModel.CodeElements;

            //Class : interfaceName yapılacak kodu bul ve bunu ekle
            foreach (CodeElement element in elements)
            {
                if (element.Kind == vsCMElement.vsCMElementNamespace)
                {
                    CodeNamespace ns = (CodeNamespace)element;
                    foreach (CodeElement elem in ns.Members)
                    {
                        if (elem is CodeClass)
                        {
                            ((CodeClass)elem).AddImplementedInterface(interfaceName, -1);
                        }
                    }
                }
            }
        }

        /// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
        /// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
        {
        }

        /// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />		
        public void OnAddInsUpdate(ref Array custom)
        {
        }

        /// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnStartupComplete(ref Array custom)
        {
        }

        /// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnBeginShutdown(ref Array custom)
        {
        }

        /// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
        /// <param term='commandName'>The name of the command to determine state for.</param>
        /// <param term='neededText'>Text that is needed for the command.</param>
        /// <param term='status'>The state of the command in the user interface.</param>
        /// <param term='commandText'>Text requested by the neededText parameter.</param>
        /// <seealso class='Exec' />
        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
            if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
            {
                if (commandName == "WebServiceGeneratorAddIn.Connect.WebServiceGeneratorAddIn")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
            }
        }

        /// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
        /// <param term='commandName'>The name of the command to execute.</param>
        /// <param term='executeOption'>Describes how the command should be run.</param>
        /// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
        /// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
        /// <param term='handled'>Informs the caller if the command was handled or not.</param>
        /// <seealso class='Exec' />
        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            handled = false;
            if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
            {
                if (commandName == "WebServiceGeneratorAddIn.Connect.WebServiceGeneratorAddIn")
                {
                    handled = true;
                    return;
                }
            }
        }
        private DTE2 _applicationObject;
        private AddIn _addInInstance;
    }
}