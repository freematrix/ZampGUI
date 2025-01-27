﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.IO;
using ZampLib.Business;
using ZampLib;
using System.Net;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Linq;
using System.IO.Compression;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.Runtime.InteropServices.ComTypes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Collections;
using Newtonsoft.Json.Linq;

namespace ZampGUI
{
    public partial class FormOneClick_WP2 : Form
    {
        #region vars
        public ConfigVar cv;
        public ComboboxItem comboBoxSelItem_Lang;
        public ComboboxItem comboBoxSelItem_WPVers;
        public bool requestAddSites = false;
        #endregion

        #region constructor
        public FormOneClick_WP2(ConfigVar cv)
        {
            InitializeComponent();
            this.cv = cv;
            ricaricaComboIstanzeWP();

            // combobox language
            var list = listLocale();
            foreach ( var item in list )
            {
                comboBoxLang.Items.Add(item);
            }

            var bindingSource1 = new BindingSource();
            bindingSource1.DataSource = list;
            comboBoxLang.DataSource = bindingSource1.DataSource;
            comboBoxLang.ValueMember = "Value";
            comboBoxLang.DisplayMember = "Text";
            comboBoxLang.SelectedItem = (from x in list where x.Value == cv.wpop.comboBoxLang select x).First();

            // combobox wp version
            var lista2 = listVersioni();
            foreach (var item in lista2)
            {
                comboBoxWPVersion.Items.Add(item);
            }
            var bindingSource2 = new BindingSource();
            bindingSource2.DataSource = lista2;
            comboBoxWPVersion.DataSource = bindingSource2.DataSource;
            comboBoxWPVersion.ValueMember = "Value";
            comboBoxWPVersion.DisplayMember = "Text";
            if(comboBoxWPVersion.Items.Count > 0 && !string.IsNullOrEmpty(cv.wpop.comboBoxWPVersion))
            {
                var itemtest = (from x in lista2 where x.Value == cv.wpop.comboBoxWPVersion select x).FirstOrDefault();
                if(itemtest != null)
                {
                    comboBoxWPVersion.SelectedItem = itemtest;//(from x in lista2 where x.Value == cv.wpop.comboBoxWPVersion select x).First();
                }
            }
            


            //default values
            txt_User.Text = cv.wpop.txt_User;
            txt_Pwd.Text = cv.wpop.txt_Pwd;
            txt_DisplayName.Text = cv.wpop.txt_DisplayName;
            txt_Email.Text = cv.wpop.txt_Email;
            

        }
        #endregion

        #region eventi form
        private async void btnInstall_Click(object sender, EventArgs e)
        {
            txtOut.Text = "";
            string nome_sito = txt_nomesito.Text.ToLower().Trim();
            string user = txt_User.Text.ToLower().Trim();
            string pwd = txt_Pwd.Text.ToLower().Trim();
            string email = txt_Email.Text.ToLower().Trim();
            string displayname = txt_DisplayName.Text.ToLower().Trim();
            string path_folder_wp = Path.Combine(cv.Apache_htdocs_path, nome_sito);
            string path_wp = Path.Combine(cv.App_Path, cv.pathWPcli, "wp.bat");
            this.comboBoxSelItem_Lang = (ComboboxItem)comboBoxLang.SelectedItem;
            this.comboBoxSelItem_WPVers = (ComboboxItem)comboBoxWPVersion.SelectedItem;


            string sout_err = "";
            sout_err += controllaCampo(nome_sito, "name website");
            sout_err += controllaCampo(user, "user");
            sout_err += controllaCampo(pwd, "password");
            sout_err += controllaCampo(email, "email", true);
            sout_err += controllaCampo(displayname, "nickname");

            if (!string.IsNullOrEmpty(sout_err))
            {
                MessageBox.Show(sout_err);
                return;
            }

            
            if (cv.wpop.checkOverwriteWebSite)
            {
                //cancello tutto db e cartella
                cancellaIstanzaWp(nome_sito);
            }
            else
            {
                // ho richiesto di non sovrascrivere controllo se esiste db e cartella sito
                List<string> all_db = ZampGUILib.getAllDB(cv.mariadb_port);
                foreach (string s in all_db)
                {
                    if (s.Equals(nome_sito))
                    {
                        MessageBox.Show("database \"" + s + "\" already exists - please insert a different name");
                        return;
                    }
                }

                if (System.IO.Directory.Exists(path_folder_wp))
                {
                    MessageBox.Show("folder \"" + nome_sito + "\" already exists inside Apache/htdocs - please use a different name");
                    return;
                }
            }

            Directory.CreateDirectory(path_folder_wp);
            disabilita_comandi_form();
            txtOut.Text = "Downloading Wordpress ...." + Environment.NewLine;
            Application.DoEvents();


            //sezione in cui avvio il job in un altro thread
            var progress = new Progress<string>(arg => txtOut.Text += arg + Environment.NewLine);
            await Task.Factory.StartNew(() => this.esegui_worker(progress), TaskCreationOptions.LongRunning);

            //finito job asincrono
            abilita_comandi_form();
            ricaricaComboIstanzeWP();

            //carico la lista dei siti salvati
            string fullurl = "http://localhost/" + nome_sito;
            if(this.cv.listaSites.Where(x => x.Url.Equals(fullurl, StringComparison.CurrentCultureIgnoreCase)).Count() == 0)
            {
                DialogResult dialogResult2 = MessageBox.Show("Would like to add http://localhost/" + nome_sito + " to \"Link -> Sites\" Menu?", "Wordpress Installation OK", MessageBoxButtons.YesNo);
                if (dialogResult2 == DialogResult.Yes)
                {
                    this.cv.listaSites.Add(new RigaSite() { Name = nome_sito, Url = fullurl });
                    requestAddSites = true;
                }
            }

            DialogResult dialogResult = MessageBox.Show("Completed " + Environment.NewLine + "Would like to visit http://localhost/" + nome_sito, "Wordpress Installation OK", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start("http://localhost/" + nome_sito);
            }
            
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            txtOut.Text = "";
            string nomeistanza = comboBoxWpDelete.SelectedItem.ToString();

            if (string.IsNullOrEmpty(nomeistanza))
            {
                MessageBox.Show("please choose a web site from combobox");
                return;
            }
            disabilita_comandi_form();
            cancellaIstanzaWp(nomeistanza);
            abilita_comandi_form();
            txtOut.Text = "Wordpress \"" + nomeistanza + "\" deleted";
            ricaricaComboIstanzeWP();
        }

        private void btnBackup_Click(object sender, EventArgs e)
        {
            txtOut.Text = "";
            string enviromentPath = cv.PHP_path + ";" + Path.Combine(cv.MariaDB_path, "bin") + ";" + System.Environment.GetEnvironmentVariable("PATH");
            string nomeistanza = comboBoxWpBackup.SelectedItem.ToString();
            string path_folder_wp = Path.Combine(cv.Apache_htdocs_path, nomeistanza);
            string path_folder_zip = Path.Combine(cv.Apache_htdocs_path, nomeistanza + "_" + DateTime.Now.ToString("yyyy-MM-dd_-_HH-mm-ss") + ".zip");
            string comando_backup_db = "mariadb-dump -u root --password=root " + nomeistanza + " > \"" + Path.Combine(path_folder_wp, nomeistanza) + ".sql\"";

            if(string.IsNullOrEmpty(nomeistanza))
            {
                MessageBox.Show("please choose a web site from the combobox on the left");
                return;
            }

            disabilita_comandi_form();

            string output = eseguiComando(comando_backup_db, path_folder_wp, enviromentPath);

            if (File.Exists(path_folder_zip))
                File.Delete(path_folder_zip);

            ZipFile.CreateFromDirectory(path_folder_wp, path_folder_zip);
            output += "Zip file \"" + path_folder_zip + "\"";
            abilita_comandi_form();

            string msg = "File saved inside " + cv.Apache_htdocs_path + Environment.NewLine + "with the following name" + Environment.NewLine + Path.GetFileName(path_folder_zip);
            txtOut.Text = output + Environment.NewLine + msg;
            MessageBox.Show(msg);
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            txtOut.Text = "";
            string enviromentPath = cv.PHP_path + ";" + Path.Combine(cv.MariaDB_path, "bin") + ";" + System.Environment.GetEnvironmentVariable("PATH");
            string nomeistanza = (comboBoxWpRestore.SelectedItem != null? comboBoxWpRestore.SelectedItem.ToString(): comboBoxWpRestore.Text);
            string path_folder_wp = Path.Combine(cv.Apache_htdocs_path, nomeistanza);

            string comando_sql1 = "mariadb -u root --password=root -e \"DROP DATABASE IF EXISTS " + nomeistanza + ";CREATE DATABASE " + nomeistanza + " DEFAULT CHARACTER SET utf8 COLLATE utf8_unicode_ci;\"";
            string comando_sql2 = "mariadb -u root --password=root " + nomeistanza + " < \"" + Path.Combine(path_folder_wp, nomeistanza) + ".sql\"";
            

            if (string.IsNullOrEmpty(nomeistanza))
            {
                MessageBox.Show("please choose a web site from the combobox on the left");
                return;
            }

            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                InitialDirectory = cv.Apache_htdocs_path,
                Title = "Browse Zip Files",
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "zip",
                Filter = "zip file (*.zip)|*.zip",
                FilterIndex = 2,
                RestoreDirectory = true,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure you want to restore " + Path.GetFileName(openFileDialog1.FileName) + " inside " + Path.GetFileName(path_folder_wp) + "?", "Restore Confirm Operation", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    disabilita_comandi_form();

                    //cancello istanza nel caso esista già
                    cancellaIstanzaWp(nomeistanza);

                    ZipFile.ExtractToDirectory(openFileDialog1.FileName, path_folder_wp);
                    txtOut.Text += "Folder \"" + path_folder_wp + "\" created";
                    txtOut.Text += eseguiComando(comando_sql1, path_folder_wp, enviromentPath);
                    txtOut.Text += eseguiComando(comando_sql2, path_folder_wp, enviromentPath);

                    abilita_comandi_form();

                    ricaricaComboIstanzeWP();

                    string msg = "Wordpress \"" + nomeistanza + "\" restored inside folder \"" + path_folder_wp + "\"";
                    txtOut.Text += msg;
                    MessageBox.Show(msg);
                }

                
            }

        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            FormOneClick_WP_Options frm_op = new FormOneClick_WP_Options(cv);
            frm_op.ShowDialog(this);
            //if(frm_op.ButtonPressed == "OK")
            //{
            //    this.wpop = frm_op.wpop;
            //}
            frm_op.Dispose();
        }
        private void FormOneClick_WP2_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.cv.wpop.txt_User = txt_User.Text.Trim();
            this.cv.wpop.txt_Pwd = txt_Pwd.Text.Trim();
            this.cv.wpop.txt_Email = txt_Email.Text.Trim();
            this.cv.wpop.txt_DisplayName = txt_DisplayName.Text.Trim();

            this.comboBoxSelItem_Lang = (ComboboxItem)comboBoxLang.SelectedItem;
            this.comboBoxSelItem_WPVers = (ComboboxItem)comboBoxWPVersion.SelectedItem;

            this.cv.wpop.comboBoxLang = this.comboBoxSelItem_Lang.Value.ToString();
            this.cv.wpop.comboBoxWPVersion = this.comboBoxSelItem_WPVers.Value.ToString();
        }
        #endregion

        #region metodi background worker
        private void esegui_worker(IProgress<string> progress)
        {
            //main form
            string nome_sito = txt_nomesito.Text.ToLower().Trim();
            string user = txt_User.Text.ToLower().Trim();
            string pwd = txt_Pwd.Text.ToLower().Trim();
            string email = txt_Email.Text.ToLower().Trim();
            string displayname = txt_DisplayName.Text.ToLower().Trim();
            string lang = this.comboBoxSelItem_Lang.Value.ToString();
            string wpvers = this.comboBoxSelItem_WPVers.Value.ToString();
            //string theme = (from x in cv.Vers_WP where x.num == wpvers select x.theme).SingleOrDefault(); //unused, right now i'm using --all option from theme delete - delete all theme except the active one


            //options
            string sitename = cv.wpop.txtSiteName;
            sitename = string.IsNullOrEmpty(sitename) ? nome_sito : sitename;


            //paths
            string path_folder_wp = Path.Combine(cv.Apache_htdocs_path, nome_sito);
            string path_wp = "\"" +  Path.Combine(cv.App_Path, cv.pathWPcli, "wp.bat") + "\"";
            string enviromentPath = cv.PHP_path + ";" + Path.Combine(cv.MariaDB_path, "bin") + ";" + System.Environment.GetEnvironmentVariable("PATH");

            //all comands
            List<string> comandi_create_wp_cli = new List<string>();

            if(wpvers == "latest")
            {
                comandi_create_wp_cli.Add(path_wp + " core download");
            }
            else
            {
                comandi_create_wp_cli.Add(path_wp + " core download --version=" + wpvers);
            }
            comandi_create_wp_cli.Add(path_wp + " config create --dbname=\"" + nome_sito + "\" --dbuser=root --dbpass=root");
            if(cv.wpop.checkEnableWPPostRevision)
            {
                comandi_create_wp_cli.Add(path_wp + " config set WP_POST_REVISIONS " + cv.wpop.txtNumericPostRevision + " --raw");
            }
            if (cv.wpop.checkDisableAutoUpdate)
            {
                comandi_create_wp_cli.Add(path_wp + " config set WP_AUTO_UPDATE_CORE false --raw");
            }
            comandi_create_wp_cli.Add(path_wp + " db create");
            comandi_create_wp_cli.Add(path_wp + " core install --url=\"http://localhost/" + nome_sito + "\" --title=\"" + sitename + "\" --admin_user=\"" + user + "\" --admin_password=\"" + pwd + "\" --admin_email=\"" + email + "\"");
            comandi_create_wp_cli.Add(path_wp + " user update 1 --nickname=\"" + displayname + "\" --display_name=\"" + displayname + "\"");
            if (lang != cv.wpop.comboBoxLang)
            {
                comandi_create_wp_cli.Add(path_wp + " language core install " + lang);
                comandi_create_wp_cli.Add(path_wp + " site switch-language " + lang);
            }
            if(cv.wpop.checkRemoveDefaultPlugin)
            {
                comandi_create_wp_cli.Add(path_wp + " plugin delete hello akismet");
            }
            if(cv.wpop.checkRemoveDefaultTheme)
            {
                comandi_create_wp_cli.Add(path_wp + " theme delete --all");
            }
            if (!string.IsNullOrEmpty(cv.wpop.txtDescription))
            {
                comandi_create_wp_cli.Add(path_wp + " option update blogdescription \"" + cv.wpop.txtDescription + "\"");
            }
            if (!string.IsNullOrEmpty(cv.wpop.txtPluginList))
            {
                comandi_create_wp_cli.Add(path_wp + " plugin install " + cv.wpop.txtPluginList);
            }
            if (!string.IsNullOrEmpty(cv.wpop.txtThemeList))
            {
                comandi_create_wp_cli.Add(path_wp + " theme install " + cv.wpop.txtThemeList);
            }

            
            //{
            //    comandi_create_wp_cli.Add(path_wp + " option update timezone_string \"Europe/Rome\"");
            //    comandi_create_wp_cli.Add(path_wp + " option update time_format \"H:i\"");
            //    comandi_create_wp_cli.Add(path_wp + " option update rss_use_excerpt 1");
            //    comandi_create_wp_cli.Add(path_wp + " option update uploads_use_yearmonth_folders 0");
            //    comandi_create_wp_cli.Add(path_wp + " rewrite structure /%postname%/ --hard");
            //}


            if (!string.IsNullOrEmpty(cv.wpop.timezone_string))
            {
                comandi_create_wp_cli.Add(path_wp + " option update timezone_string \"" + cv.wpop.timezone_string + "\"");
            }
            if (!string.IsNullOrEmpty(cv.wpop.time_format))
            {
                comandi_create_wp_cli.Add(path_wp + " option update time_format \"" + cv.wpop.time_format + "\"");
            }
            if (!string.IsNullOrEmpty(cv.wpop.rss_use_excerpt))
            {
                comandi_create_wp_cli.Add(path_wp + " option update rss_use_excerpt " + cv.wpop.rss_use_excerpt);
            }
            if (!string.IsNullOrEmpty(cv.wpop.uploads_use_yearmonth_folders))
            {
                comandi_create_wp_cli.Add(path_wp + " option update uploads_use_yearmonth_folders " + cv.wpop.uploads_use_yearmonth_folders);
            }
            if (!string.IsNullOrEmpty(cv.wpop.permalink_string))
            {
                comandi_create_wp_cli.Add(path_wp + " rewrite structure " + cv.wpop.permalink_string + " --hard");
            }



            foreach (string comando in comandi_create_wp_cli)
            {
                string output = eseguiComando(comando, path_folder_wp, enviromentPath);
                progress.Report(output);
            }


            // ------------------------------------------------------------------------------------------------
            //creating dummy post
            if (cv.wpop.txtNumPost > 0)
            {
                //creating 3 categories
                string idcat = eseguiComando(path_wp + " term create category cat1 --porcelain", path_folder_wp, enviromentPath);
                string idimg = eseguiComando(path_wp + " media import " + "\"" + Path.Combine(cv.App_Path, cv.pathWPcli, "imgplaceholder.png") + "\"" + " --porcelain", path_folder_wp, enviromentPath);

                // cleaning the output
                idcat = idcat.Trim();
                idimg = idimg.Trim();


                for (int numpost = 0; numpost < cv.wpop.txtNumPost; numpost++)
                {
                    //get content and excerpt
                    string content = ZampGUILib.getRandomLorem();
                    string excerpt = "";
                    foreach (var s in content.Split(' '))
                    {
                        excerpt += s + " ";
                        if (excerpt.Length > 100)
                        {
                            excerpt = excerpt.Trim();
                            break;
                        }
                    }

                    //create a post
                    string comando = path_wp + " post create --porcelain --post_status=publish --post_author=1 --post_title=\"Post " + (numpost + 1) + "\" --post_excerpt=\"" + excerpt + "\" --post_category=\"cat1\" --tags_input='tag" + (numpost + 1) + "' --post_content=\"" + content + "\"";
                    string idpost = eseguiComando(comando, path_folder_wp, enviromentPath);
                    idpost = idpost.Trim();

                    //attach featured image
                    comando = path_wp + " post meta update " + idpost + " _thumbnail_id " + idimg;
                    eseguiComando(comando, path_folder_wp, enviromentPath);
                }
            }

        }
        #endregion

        #region metodi privati
        private void ricaricaComboIstanzeWP()
        {
            List<string> lista_folder = (from x in System.IO.Directory.GetDirectories(cv.Apache_htdocs_path) select Path.GetFileName(x)).ToList();
            List<string> lista_db = ZampGUILib.getAllDB(cv.mariadb_port);
            var CommonList = lista_folder.Intersect(lista_db);

            comboBoxWpDelete.Items.Clear();
            comboBoxWpBackup.Items.Clear();
            comboBoxWpRestore.Items.Clear();
            comboBoxWpRestore.Text = "";

            foreach (string file in CommonList)
            {
                comboBoxWpDelete.Items.Add(Path.GetFileName(file));
                comboBoxWpBackup.Items.Add(Path.GetFileName(file));
                comboBoxWpRestore.Items.Add(Path.GetFileName(file));
            }

        }
        private void cancellaIstanzaWp(string nomeistanza)
        {
            //cancello tutto db e cartella
            string connStr = "server=127.0.0.1;user=root;password=root;port=" + cv.mariadb_port;
            using (var conn = new MySqlConnection(connStr))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText = "DROP DATABASE IF EXISTS " + nomeistanza + ";";
                cmd.ExecuteNonQuery();
            }

            //cancello la cartella dentro la httdocs
            string path_folder_wp = Path.Combine(cv.Apache_htdocs_path, nomeistanza);

            if (System.IO.Directory.Exists(path_folder_wp))
            {
                Directory.Delete(path_folder_wp, true);
            }
        }

        private void disabilita_comandi_form()
        {
            tabControl1.Enabled = false;
            Cursor.Current = Cursors.WaitCursor;
            btnInstall.Enabled = false;
            btnDelete.Enabled = false;
            btnRestore.Enabled = false;
            btnBackup.Enabled = false;
        }

        private void abilita_comandi_form()
        {
            tabControl1.Enabled = true;
            Cursor.Current = Cursors.Default;
            btnInstall.Enabled = true;
            btnDelete.Enabled = true;
            btnRestore.Enabled = true;
            btnBackup.Enabled = true;
            this.Focus();
        }
        private string eseguiComando(string comando, string currentdir, string enviromentPath)
        {
            Tuple<string, string> tt;
            Dictionary<string,string> map = new Dictionary<string,string>();
            map.Add("WP_CLI_CACHE_DIR", Path.Combine(cv.App_Path, cv.pathWPcli, ".wp-cli"));
            map.Add("WP_CLI_CONFIG_PATH", Path.Combine(cv.App_Path, cv.pathWPcli, "config.yml"));
            tt = ZampGUILib.startProc_and_wait_output2(comando, "", true, currentdir, enviromentPath, map);
            string result = tt.Item1 + Environment.NewLine + tt.Item2;
            //string result = tt.Item1;
            return result;
        }
        private string controllaCampo(string nomeCampo, string nomeEtichetta, bool b_controllomail = false)
        {
            string sout_err = "";
            if (string.IsNullOrEmpty(nomeCampo))
            {
                sout_err += "Please insert a name for \"" + nomeEtichetta + "\"" + Environment.NewLine;
            }

            if (nomeCampo == "wordpress")
            {
                sout_err += "'wordpress' name is reserved for \"" + nomeEtichetta + "\", Please use a different name" + Environment.NewLine;
            }

            if (nomeCampo.Length < 2 || nomeCampo.Length > 30)
            {
                sout_err += "Please use between 2 and 30 characters for \"" + nomeEtichetta + "\"" + Environment.NewLine;
            }


            if (b_controllomail)
            {
                if(!IsValidEmail(nomeCampo))
                {
                    sout_err += "Email invalid, please enter a valid email" + Environment.NewLine;
                }
            }
            else
            {
                if (!Regex.IsMatch(nomeCampo, @"^[a-zA-Z]+(\d)*[a-zA-Z]*$"))
                {
                    sout_err += "For \"" + nomeEtichetta + "\" please enter a name that starts with a letter followed by a letter or number" + Environment.NewLine;
                }
            }

            return sout_err;
        }

        public bool IsValidEmail(string email)
        {
            string pattern = @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|" + @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)" + @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(email);
        }

        public List<ComboboxItem> listLocale()
        {
            List<ComboboxItem> list = new List<ComboboxItem>();
            
            //https://wpastra.com/docs/complete-list-wordpress-locale-codes/

            string scodici = @"
                Afrikaans	af
                Albanian	sq
                Algerian Arabic	arq
                Akan	ak
                Amharic	am
                Arabic	ar
                Armenian	hy
                Aromanian	rup_MK
                Arpitan	frp
                Assamese	as
                Asturian	ast
                Azerbaijani	az
                Azerbaijani (Turkey)	az_TR
                Balochi Southern	bcc
                Bashkir	ba
                Basque	eu
                Belarusian	bel
                Bengali	bn_BD
                Bengali (India)	bn_IN
                Bhojpuri	bho
                Bodo	brx
                Borana-Arsi-Guji Oromo	gax
                Bosnian	bs_BA
                Breton	bre
                Bulgarian	bg_BG
                Burmese	my_MM
                Catalan	ca
                Catalan (Balear)	bal
                Cebuano	ceb
                Chinese (China)	zh_CN
                Chinese (Hong Kong)	zh_HK
                Chinese (Singapore)	zh_SG
                Chinese (Taiwan)	zh_TW
                Cornish	cor
                Corsican	co
                Croatian	hr
                Czech	cs_CZ
                Danish	da_DK
                Dhivehi	dv
                Dutch	nl_NL
                Dutch (Belgium)	nl_BE
                Dzongkha	dzo
                Emoji	art-xemoji
                English	en_US
                English (Australia)	en_AU
                English (Canada)	en_CA
                English (New Zealand)	en_NZ
                English (Pirate)	art_xpirate
                English (South Africa)	en_SA
                English (UK)	en_GB
                Esperanto	eo
                Estonian	et
                Ewe	ewe
                Faroese	fo
                Finnish	fi
                Fon	fon
                French (Belgium)	fr_BE
                French (Canada)	fr_CA
                French (France)	fr_FR
                Frisian	fy
                Friulian	fur
                Fulah	fuc
                Galician	gl_ES
                Georgian	ka_GE
                German	de_DE
                German (Austria)	de_AT
                German (Switzerland)	de_CH
                Greek	el
                Greenlandic	kal
                Guaraní	gn
                Gujarati	gu_IN
                Hawaiian	haw_US
                Haitian Creole	hat
                Hausa	hau
                Hazaragi	haz
                Hebrew	he_IL
                Hindi	hi_IN
                Hungarian	hu_HU
                Icelandic	is_IS
                Ido	ido
                Igbo	ibo
                Indonesian	id_ID
                Irish	ga
                Italian	it_IT
                Japanese	ja
                Javanese	jv_ID
                Kabyle	kab
                Kannada	kn
                Karakalpak	kaa
                Kazakh	kk
                Khmer	km
                Kinyarwanda	kin
                Kirghiz	ky_KY
                Korean	ko_KR
                Kurdish (Sorani)	ckb
                Kurdish (Kurmanji)	kmr
                Kyrgyz	kir
                Lao	lo
                Latvian	lv
                Latin	la
                Ligurian	lij
                Limburgish	li
                Lingala	lin
                Lithuanian	lt_LT
                Lombard	lmo
                Lower Sorbian	dsb
                Luganda	lug
                Luxembourgish	lb_LU
                Macedonian	mk_MK
                Maithili	mai
                Malagasy	mg_MG
                Maltese	mlt
                Malay	ms_MY
                Malayalam	ml_IN
                Maori	mri
                Mauritian Creole	mfe
                Marathi	mr
                Mingrelian	xmf
                Mongolian	mn
                Montenegrin	me_ME
                Moroccan Arabic	ary
                Myanmar (Burmese)	my_MM
                Nepali	ne_NP
                Nigerian Pidgin	pcm
                N’ko	nqo
                Norwegian (Bokmål)	nb_NO
                Norwegian (Nynorsk)	nn_NO
                Occitan	oci
                Oriya	ory
                Ossetic	os
                Pashto	ps
                Panjabi (India)	pa_IN
                Papiamento (Aruba)	pap_AW
                Papiamento (Curaçao and Bonaire)	pap_CW
                Persian	fa_IR
                Persian (Afghanistan)	fa_AF
                Polish	pl_PL
                Portuguese (Angola)	pt_AO
                Portuguese (Brazil)	pt_BR
                Portuguese (Portugal)	pt_PT
                Punjabi	pa_IN
                Rohingya	rhg
                Romanian	ro_RO
                Romansh	roh
                Russian	ru_RU
                Russian (Ukraine)	ru_UA
                Rusyn	rue
                Sakha	sah
                Sanskrit	sa_IN
                Saraiki	skr
                Sardinian	srd
                Scottish Gaelic	gd
                Serbian	sr_RS
                Shona	sna
                Shqip (Kosovo)	sq_XK
                Sicilian	scn
                Sindhi	sd_PK
                Sinhala	si_LK
                Silesian	szl
                Slovak	sk_SK
                Slovenian	sl_SI
                Somali	so_SO
                South Azerbaijani	azb
                Spanish (Argentina)	es_AR
                Spanish (Chile)	es_CL
                Spanish (Costa Rica)	es_CR
                Spanish (Colombia)	es_CO
                Spanish (Dominican Republic)	es_DO
                Spanish (Ecuador)	es_EC
                Spanish (Guatemala)	es_GT
                Spanish (Honduras)	es_HN
                Spanish (Mexico)	es_MX
                Spanish (Peru)	es_PE
                Spanish (Puerto Rico)	es_PR
                Spanish (Spain)	es_ES
                Spanish (Uruguay)	es_UY
                Spanish (Venezuela)	es_VE
                Sundanese	su_ID
                Swati	ssw
                Swahili	sw
                Swedish	sv_SE
                Swiss German	gsw
                Syriac	syr
                Tagalog	tl
                Tahitian	tah
                Tajik	tg
                Tamazight (Central Atlas)	tzm
                Tamazight	zgh
                Tamil	ta_IN
                Tamil (Sri Lanka)	ta_LK
                Tatar	tt_RU
                Telugu	te
                Thai	th
                Tibetan	bo
                Tigrinya	tir
                Turkish	tr_TR
                Turkmen	tuk
                Tweants	twd
                Uighur	ug_CN
                Ukrainian	uk
                Upper Sorbian	hsb
                Urdu	ur
                Uzbek	uz_UZ
                Venetian	vec
                Vietnamese	vi
                Walloon	wa
                Welsh	cy
                Wolof	wol
                Xhosa	xho
                Yoruba	yor
                Zulu	zul
                ";

            string[] lines = scodici.Split(new string[] { Environment.NewLine },StringSplitOptions.None);
            foreach (string line in lines) 
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                string[] locale_codice = line.Split(new string[] { "\t" },StringSplitOptions.None);
                //var item = new KeyValuePair<string, string>(locale_codice[1].Trim(), locale_codice[0].Trim());

                ComboboxItem c = new ComboboxItem();
                c.Text = locale_codice[0].Trim();
                c.Value = locale_codice[1].Trim();
                list.Add(c);
            }

            return list;
        }

        public List<ComboboxItem> listVersioni()
        {
            List<ComboboxItem> list = new List<ComboboxItem>();

            foreach(SingolaVersioneWP s in cv.Vers_WP)
            {
                ComboboxItem c = new ComboboxItem();
                c.Text = s.num;
                c.Value = s.num;
                list.Add(c);
            }
            return list;
        }


        #endregion

        

        

        public class ComboboxItem
        {
            public string Text { get; set; }
            public string Value { get; set; }
        }
    }
}
