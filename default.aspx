<%@ Page Language="C#" CodeFile="WeMosDefGUI.aspx.cs" Inherits="WeMosDefGUI" AutoEventWireup="true" %>
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">
      <title>WeMosDef</title>
    <link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-alpha.6/css/bootstrap.min.css" integrity="sha384-rwoIResjU2yc3z8GV/NPeZWAv56rSmLldC3R/AZzGRnGxQQKnKkoFVhFQhNUwEyJ" crossorigin="anonymous">
	<link href="dashboard.css" rel="stylesheet">
	<link href="main.css" rel="stylesheet">
  </head>
  <body>
    <nav class="navbar navbar-toggleable-md navbar-inverse fixed-top bg-inverse">
      <button class="navbar-toggler navbar-toggler-right hidden-lg-up" type="button" data-toggle="collapse" data-target="#navbarsExampleDefault" aria-controls="navbarsExampleDefault" aria-expanded="false" aria-label="Toggle navigation">
        <span class="navbar-toggler-icon"></span>
      </button>
      <a class="navbar-brand" href="#">WeMosDef</a>

      <div class="collapse navbar-collapse" id="navbarsExampleDefault">
        <ul class="navbar-nav mr-auto">
          <li class="nav-item active">
            <a class="nav-link" href="#">Home <span class="sr-only">(current)</span></a>
          </li>
          <li class="nav-item">
            <a class="nav-link" href="#">Settings</a>
          </li>
          <li class="nav-item">
            <a class="nav-link" href="#">Profile</a>
          </li>
          <li class="nav-item">
            <a class="nav-link" href="#">Help</a>
          </li>
        </ul>
        <form class="form-inline mt-2 mt-md-0">
          <input class="form-control mr-sm-2" type="text" placeholder="Search">
          <button class="btn btn-outline-success my-2 my-sm-0" type="submit">Search</button>
        </form>
      </div>
    </nav>

      <div><%=this.PowerState %></div>

	<div class="container" style="margin: 34px 12px 12px 12px;">
		<form action="" method="post" id="f1" name="f1">
            <input type="hidden" id="action" name="action" value="" />
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('on')">On</button>
			</div>
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('off')">Off</button>
			</div>	
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('clean')">Clean Cycle</button>
			</div>					
			<div class="form-group">
				<button type="button" class="btn btn-outline-primary wemo-button" onclick="handleButton('powerstate')">Power State</button>
			</div>					
		</form>
	</div>

    <script src="https://code.jquery.com/jquery-3.1.1.slim.min.js" integrity="sha384-A7FZj7v+d/sdmMqp/nOQwliLvUsJfDHW+k9Omg/a/EheAdgtzNs3hpfag6Ed950n" crossorigin="anonymous"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/tether/1.4.0/js/tether.min.js" integrity="sha384-DztdAPBWPRXSA/3eYEEUWrWCy7G5KFbe8fFjk5JAIxUYHKkDx6Qin1DkWx51bBrb" crossorigin="anonymous"></script>
    <script src="https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-alpha.6/js/bootstrap.min.js" integrity="sha384-vBWWzlZJ8ea9aCX4pEW3rVHjgjt7zpkNpZk+02D9phzyeVkE+jo0ieGizqPLForn" crossorigin="anonymous"></script>
	<script>
		function handleButton(action) {
			switch(action) {
				case 'on':
					break;
				case 'off':
					break;
				case 'clean':
					break;
				default:
					break;
				
			}
			$('#action').val(action);
			$('#f1').submit();
        }
	</script>
  </body>
</html>