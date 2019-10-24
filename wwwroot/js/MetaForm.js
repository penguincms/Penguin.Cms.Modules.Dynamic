jQuery.fn.tagName = function () {
    return this.prop("tagName").toLowerCase();
};

var KeyProvider = {
    Element,
    SaveFunctions: [],
    Submit: function () {
        Site.ShowLoader();
        var i = 0;
        var success = false;
        for (i = 0; i < this.SaveFunctions.length; i++) {
            if (this.SaveFunctions[i].KeyFunction) {
                success = this.SaveFunctions[i].ToExecute();
                break;
            }
        }

        if (success) {
            for (i = 0; i < this.SaveFunctions.length; i++) {
                if (!this.SaveFunctions[i].KeyFunction) {
                    var r = this.SaveFunctions[i].ToExecute();

                    if (typeof r !== 'undefined') {
                        success = success && r;
                    }
                }
            }
        } else {
            alert('Object key save error occured');
        }

        for (i = 0; i < this.SaveFunctions.length; i++) {
            if (this.SaveFunctions[i].Callback) {
                this.SaveFunctions[i].Callback(success);
            }
        }

        Site.HideLoader();
    },
    AddFunction: function (func, keyFunc, callback) {
        this.SaveFunctions.push({
            ToExecute: func,
            KeyFunction: !!keyFunc,
            Callback : callback
        });
    }
};

$(document).ready(function () {
    $('.toggleProperty').change(function () {
        if ($(this).is(":checked")) {
            $(this).closest('property').attr('data-disabled', 'False');
            $(this).closest('property').attr('data-ignored', 'False');
            $(this).closest('property').children('value').show("slide", { direction: "up" }, "slow");
        } else {
            $(this).closest('property').attr('data-disabled', 'True');
            $(this).closest('property').attr('data-ignored', 'True');
            $(this).closest('property').children('value').hide("slide", { direction: "up" }, "slow");
        }
    });

    Meta.AttachSearchHandler();

});

//Move this into the meta object
function CopyTemplate(element) {
    var template = $(element).closest('property').children('value').children('template');

    var thtml = $(template).html();

    var newTemplate = $(thtml);

    newTemplate = $(thtml);

    $(template).closest('[data-propertyname]').children('value').children('.valuesList').append(newTemplate);

    newTemplate.show();

    Meta.AttachSearchHandler();
}

var Meta = {
    AttachSearchHandler: function () {
        $('.TypeSearch').each(function (i, e) {
            var url = $(this).attr('data-searchurl');

            var icon = $(e).siblings('i')[0];

            var property = $(e).closest('property');

            if (property.hasClass('wired')) {
                return;
            }

            property.addClass('wired');

            var singleSelect = property.attr('data-coretype') !== 'Collection';

            $(e).autocomplete({
                source: function (request, response) {
                    $(icon).text('av_timer');

                    var SearchType = '';
                    if (property.attr('data-coretype') === 'Collection') {
                        SearchType = property.attr('data-collectiontype');
                    } else {
                        SearchType = property.attr('data-propertytype');
                    }

                    $.ajax({
                        url: url.replace("{0}", SearchType),
                        dataType: "json",
                        data: {
                            term: request.term
                        },
                        success: function (data) {
                            var result = [];

                            var lastType = '';
                            var showType = false;

                            for (var indexi = 0; indexi < data.length; indexi++) {
                                var thisType = data[indexi].TypeName.split('.').slice(-1)[0];

                                if (lastType.length && lastType !== thisType) {
                                    showType = true;
                                    break;
                                }

                                lastType = thisType;
                            }
                            // load up a new list to return
                            for (var index = 0; index < data.length; index++) {
                                var o = data[index];

                                var val = o[property.find('.selectorValues').attr('data-value')];
                                var lab = o[property.find('.selectorValues').attr('data-label')];

                                if (typeof val === 'object' && val !== null) {
                                    val = val.$ToString;
                                }

                                if (typeof lab === 'object' && lab !== null) {
                                    lab = lab.$ToString;
                                }

                                if (showType) {
                                    lab = lab + ' (' + o.TypeName.split('.').slice(-1)[0] + ')';
                                }

                                result.push({
                                    id: val,
                                    label: lab,
                                    type: o.TypeName
                                });
                            }

                            $(icon).text('search');
                            response(result);
                        }
                    });
                },
                minLength: 2,
                select: function (event, ui) {
                    var template = property.find('template');

                    var toAdd = $(template.html());

                    toAdd.find('input').val(ui.item.id);
                    toAdd.find('label').html(ui.item.label);

                    if (singleSelect) {
                        property.find('.selectorValues').html(toAdd);
                        property.find('.searchContainer').hide();
                    } else {
                        property.find('.selectorValues').append(toAdd);
                    }

                    $(e).autocomplete('close').val('');
                    return false;
                }
            });
            property.find('.selectorValues').on('DOMNodeInserted', function (e) {
                if (singleSelect) {
                    property.find('.searchContainer').hide();
                }
            });

            property.find('.selectorValues').on('DOMNodeRemoved', function (e) {
                //Node removed after call so 1 means will be 0
                if (singleSelect && property.find('.selectorValues > *').length === 1) {
                    property.find('.searchContainer').show();
                }
            });

            if (singleSelect && property.find('.selectorValues > *').length > 0) {
                property.find('.searchContainer').hide();
            }
        });
    },
    Root: function (Id) {
        return $('#' + Id + ' property[data-isroot="True"]')[0];
    },
    RootContainer: function (Id) {
        var root = $('#' + Id + ' property[data-isroot="True"]')[0];

        if (root.closest('root')) {
            return root.closest('root');
        } else {
            return $('#' + Id + ' property[data-isroot="True"]')[0];
        }
    },
    GetJson: function (Id) {
        return this.BuildReference(this.Root(Id));
    },
    Submit: function (Url, Id, asForm) {
        if ($('#' + Id).length > 0 && !$('#' + Id)[0].reportValidity()) {
            return;
        }

        if (asForm) {
            dataType = 'application/x-www-form-urlencoded; charset=utf-8';
            data = { json: JSON.stringify(Meta.GetJson(Id)) };
        } else {
            dataType = 'application/json; charset=utf-8';
            data = JSON.stringify(Meta.GetJson(Id));
        }

        var success = false;
        $.ajax({
            type: "POST",
            url: Url,
            contentType: dataType,
            data: data,
            async: false,
            success: function (data) {
                if (data.response) {
                    if (data.response.redirect) {
                        window.location = data.response.redirect;
                    } else if (data.response.Error) {
                        $(Meta.RootContainer(Id)).replaceWith(data.response.error);
                        success = false;
                    } else {
                        $(Meta.RootContainer(Id)).replaceWith(data.response.body);
                        success = true;
                    }
                } else {
                    $(Meta.RootContainer(Id)).replaceWith(data);
                    success = true;
                }
            },
            failure: function (data) {
                success = false;
                form.find('.ErrorMessage').html(data);
                $(button).prop("true", true);
            }
        });

        return success;
    },
    BuildReference: function (e) {
        var r = {};
        var p = this.GetProperties(e);

        $.each(p, function (ii, ie) {
            r[$(ie).attr('data-propertyname')] = Meta.Build(ie);
        });

        if (p.length) {
            return r;
        } else {
            return null;
        }
    },
    BuildArray: function (e) {
        var a = [];

        if (this.GetProperties(e).length > 0) {
            $.each(this.GetProperties(e), function (ii, ie) {
                a.push(Meta.Build(ie));
            });
        } else {
            $(e).find('input:not([data-ignored="True"])').each(function (i, el) {
                a.push($(el).val());
            });
        }

        return a;
    },
    IsNotIgnored: function (i, e) {
        return !($(e).attr('data-ignored') === 'True');
    },
    GetValue: function (e) {
        var valtypes = 'input, textarea, select';
        if ($(e).find(valtypes).filter(Meta.IsNotIgnored).length) {
            return $(e).find(valtypes).filter(Meta.IsNotIgnored).val();
        } else if ($(e).find('value').filter(Meta.IsNotIgnored).length) {
            return $(e).find('value').filter(Meta.IsNotIgnored).html().trim();
        } else if ($(e).attr('data-propertytype') === 'bool') {
            return $(e).find(':checked').filter(Meta.IsNotIgnored).length > 0;
        } else {
            return null;
        }
    },
    Build: function (e) {
        var check = $(e).attr('data-coretype');

        //Do an override if it appears as though the reference was populated as a value.
        //This can occur if the custom get-set logic is needed for an object
        if ($(e).find('property').length === 0 && $(e).find('value').length === 1 && ($(e).find('template').length === 0 || $(e).attr('data-coretype') === 'Reference') && check !== 'Value') {
            check = 'Value';
        }

        switch (check) {
            case 'Reference':
                return Meta.BuildReference(e);
            case 'Collection':
                return Meta.BuildArray(e);
            default:
                return Meta.GetValue(e);
        }
    },
    GetTags: function GetTags(n, tag) {
        var p = [];

        $(n).children().each(function (i, e) {
            if ($(e).tagName === 'template' || !Meta.IsNotIgnored(0, e)) {
                return;
            }

            if ($(e).tagName() === tag) {
                p.push(e);
            } else {
                $.each(GetTags(e, tag), function (ii, ie) {
                    p.push(ie);
                });
            }
        });

        return p;
    },
    GetProperties: function (n) {
        return this.GetTags(n, 'property');
    }
};